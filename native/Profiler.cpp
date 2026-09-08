#include "CorProfilerCompat.h"

#include <bcrypt.h>
#include <windows.h>
#include <tlhelp32.h>

#include <algorithm>
#include <chrono>
#include <cstdint>
#include <cstring>
#include <cwchar>
#include <iomanip>
#include <limits>
#include <map>
#include <new>
#include <set>
#include <sstream>
#include <string>
#include <vector>

#pragma comment(lib, "bcrypt.lib")

namespace
{
    const wchar_t *const kCoreName = L"OverWolf.Client.Core.dll";
    const wchar_t *const kTargetType = L"OverWolf.Client.Core.ODKv2.Profile.OverwolfSubscription";
    const wchar_t *const kReviewedCoreHash = L"9DA15E0CACF446E59B3F728BA78F5CC8F0EC6D6616BB0F490BF90ABD21EF098E";

    // The adapter is intentionally pinned to the reviewed Core image. These
    // tokens are checked again by name before any method body is replaced.
    const mdMethodDef kDetailedMethod = 0x060030B7;
    const mdMethodDef kIdsMethod = 0x060030B6;
    const mdMethodDef kUidGetter = 0x06002ABB;
    const mdToken kStringEquality = 0x0A00033B;
    const mdToken kPlanConstructor = 0x0A0024F2;
    const mdToken kPlanIdSetter = 0x0A0024F3;
    const mdToken kStateSetter = 0x0A0024F5;
    const mdToken kExpirySetter = 0x0A0024F8;
    const mdToken kTitleSetter = 0x0A0024FB;
    const mdToken kDescriptionSetter = 0x0A0024FD;
    const mdToken kPriceSetter = 0x0A0024FF;
    const mdToken kPeriodSetter = 0x0A002501;

    const GUID kExpectedMvid =
        { 0xc8cba78c, 0xaad4, 0x41a2, { 0x9c, 0x0e, 0x1f, 0x19, 0xce, 0x2a, 0xe5, 0x38 } };

    volatile LONG g_objectCount = 0;
    volatile LONG g_serverLocks = 0;
    CRITICAL_SECTION g_logLock;
    bool g_logLockReady = false;

    std::wstring HexValue(ULONGLONG value)
    {
        std::wstringstream stream;
        stream << L"0x" << std::hex << std::uppercase << value;
        return stream.str();
    }

    std::wstring Env(const wchar_t *name)
    {
        DWORD length = GetEnvironmentVariableW(name, nullptr, 0);
        if (length == 0) return std::wstring();
        std::vector<wchar_t> value(length, L'\0');
        DWORD written = GetEnvironmentVariableW(name, value.data(), length);
        if (written == 0 || written >= length) return std::wstring();
        return std::wstring(value.data(), written);
    }

    std::wstring Lower(std::wstring value)
    {
        std::transform(value.begin(), value.end(), value.begin(), towlower);
        return value;
    }

    std::wstring BaseName(const std::wstring &path)
    {
        const size_t slash = path.find_last_of(L"\\/");
        return slash == std::wstring::npos ? path : path.substr(slash + 1);
    }

    std::wstring CurrentProcessImage()
    {
        WCHAR path[4096]{};
        const DWORD written = GetModuleFileNameW(nullptr, path, ARRAYSIZE(path));
        return written == 0 ? std::wstring() : std::wstring(path, written);
    }

    DWORD ParentProcessId()
    {
        HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE) return 0;
        PROCESSENTRY32W entry{};
        entry.dwSize = sizeof(entry);
        const DWORD current = GetCurrentProcessId();
        DWORD parent = 0;
        if (Process32FirstW(snapshot, &entry))
        {
            do
            {
                if (entry.th32ProcessID == current)
                {
                    parent = entry.th32ParentProcessID;
                    break;
                }
            } while (Process32NextW(snapshot, &entry));
        }
        CloseHandle(snapshot);
        return parent;
    }

    std::wstring LoadedClrPath()
    {
        HMODULE clr = GetModuleHandleW(L"clr.dll");
        if (!clr) clr = GetModuleHandleW(L"mscorwks.dll");
        if (!clr) return std::wstring(L"unavailable");
        WCHAR path[4096]{};
        const DWORD written = GetModuleFileNameW(clr, path, ARRAYSIZE(path));
        return written == 0 ? std::wstring(L"unavailable") : std::wstring(path, written);
    }

    bool IsTrackedModule(const std::wstring &path)
    {
        const std::wstring name = Lower(BaseName(path));
        return name == Lower(kCoreName) || name == L"overwolf.cef.dll";
    }

    std::wstring ProcessLogPath()
    {
        const std::wstring base = Env(L"OVERWOLF_PATCHER_PROFILER_LOG");
        if (base.empty() || Lower(Env(L"OVERWOLF_PATCHER_PROFILER_LOG_PER_PROCESS")) != L"1") return base;
        const size_t slash = base.find_last_of(L"\\/");
        const size_t dot = base.find_last_of(L'.');
        const bool hasExtension = dot != std::wstring::npos && (slash == std::wstring::npos || dot > slash);
        const std::wstring stem = hasExtension ? base.substr(0, dot) : base;
        const std::wstring extension = hasExtension ? base.substr(dot) : L".log";
        return stem + L"." + std::to_wstring(GetCurrentProcessId()) + extension;
    }

    void Log(const std::wstring &message)
    {
        const std::wstring path = ProcessLogPath();
        if (path.empty()) return;
        if (!g_logLockReady) return;
        EnterCriticalSection(&g_logLock);
        HANDLE file = CreateFileW(path.c_str(), FILE_APPEND_DATA,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr,
            OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (file != INVALID_HANDLE_VALUE)
        {
            SYSTEMTIME now{};
            GetSystemTime(&now);
            wchar_t prefix[64]{};
            swprintf_s(prefix, L"%04u-%02u-%02uT%02u:%02u:%02u.%03uZ ",
                now.wYear, now.wMonth, now.wDay, now.wHour, now.wMinute,
                now.wSecond, now.wMilliseconds);
            std::wstring line = std::wstring(prefix) + message + L"\r\n";
            int bytes = WideCharToMultiByte(CP_UTF8, 0, line.c_str(),
                static_cast<int>(line.size()), nullptr, 0, nullptr, nullptr);
            if (bytes > 0)
            {
                std::vector<char> utf8(static_cast<size_t>(bytes));
                WideCharToMultiByte(CP_UTF8, 0, line.c_str(),
                    static_cast<int>(line.size()), utf8.data(), bytes, nullptr, nullptr);
                DWORD written = 0;
                WriteFile(file, utf8.data(), static_cast<DWORD>(utf8.size()), &written, nullptr);
            }
            CloseHandle(file);
        }
        LeaveCriticalSection(&g_logLock);
    }

    std::wstring HResult(HRESULT hr)
    {
        std::wstringstream stream;
        stream << L"0x" << std::hex << std::uppercase << static_cast<unsigned long>(hr);
        return stream.str();
    }

    std::wstring HashFile(const std::wstring &path)
    {
        HANDLE file = CreateFileW(path.c_str(), GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr,
            OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (file == INVALID_HANDLE_VALUE) return std::wstring();

        BCRYPT_ALG_HANDLE algorithm = nullptr;
        BCRYPT_HASH_HANDLE hash = nullptr;
        DWORD objectLength = 0;
        DWORD resultLength = 0;
        std::wstring result;
        if (SUCCEEDED(BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0)) &&
            SUCCEEDED(BCryptGetProperty(algorithm, BCRYPT_OBJECT_LENGTH,
                reinterpret_cast<PUCHAR>(&objectLength), sizeof(objectLength), &resultLength, 0)))
        {
            std::vector<BYTE> object(objectLength);
            BYTE digest[32]{};
            if (SUCCEEDED(BCryptCreateHash(algorithm, &hash, object.data(), objectLength,
                    nullptr, 0, 0)))
            {
                BYTE buffer[64 * 1024];
                DWORD read = 0;
                bool okay = true;
                while (ReadFile(file, buffer, sizeof(buffer), &read, nullptr) && read > 0)
                {
                    if (FAILED(BCryptHashData(hash, buffer, read, 0)))
                    {
                        okay = false;
                        break;
                    }
                }
                if (okay && SUCCEEDED(BCryptFinishHash(hash, digest, sizeof(digest), 0)))
                {
                    std::wstringstream stream;
                    stream << std::uppercase << std::hex << std::setfill(L'0');
                    for (BYTE byte : digest) stream << std::setw(2) << static_cast<unsigned>(byte);
                    result = stream.str();
                }
                BCryptDestroyHash(hash);
            }
        }
        if (algorithm) BCryptCloseAlgorithmProvider(algorithm, 0);
        CloseHandle(file);
        return result;
    }

    bool ReadU16(const std::vector<BYTE> &data, size_t at, USHORT &value)
    {
        if (at + 2 > data.size()) return false;
        value = static_cast<USHORT>(data[at] | (static_cast<USHORT>(data[at + 1]) << 8));
        return true;
    }

    bool ReadU32(const std::vector<BYTE> &data, size_t at, ULONG &value)
    {
        if (at + 4 > data.size()) return false;
        value = static_cast<ULONG>(data[at]) |
            (static_cast<ULONG>(data[at + 1]) << 8) |
            (static_cast<ULONG>(data[at + 2]) << 16) |
            (static_cast<ULONG>(data[at + 3]) << 24);
        return true;
    }

    void PutU16(std::vector<BYTE> &data, USHORT value)
    {
        data.push_back(static_cast<BYTE>(value));
        data.push_back(static_cast<BYTE>(value >> 8));
    }

    void PutU32(std::vector<BYTE> &data, ULONG value)
    {
        data.push_back(static_cast<BYTE>(value));
        data.push_back(static_cast<BYTE>(value >> 8));
        data.push_back(static_cast<BYTE>(value >> 16));
        data.push_back(static_cast<BYTE>(value >> 24));
    }

    void PutToken(std::vector<BYTE> &data, mdToken token) { PutU32(data, token); }

    size_t Align4(size_t value) { return (value + 3u) & ~static_cast<size_t>(3u); }

    struct ExceptionClause
    {
        ULONG flags;
        ULONG tryOffset;
        ULONG tryLength;
        ULONG handlerOffset;
        ULONG handlerLength;
        ULONG classOrFilter;
    };

    struct MethodBody
    {
        size_t codeOffset = 0;
        ULONG codeSize = 0;
        USHORT maxStack = 8;
        mdToken localSignature = 0;
        bool moreSections = false;
        bool initLocals = false;
        std::vector<ExceptionClause> clauses;
    };

    bool ParseMethodBody(const std::vector<BYTE> &body, MethodBody &parsed)
    {
        if (body.empty()) return false;
        const BYTE format = body[0] & 3;
        if (format == 2)
        {
            parsed.codeOffset = 1;
            parsed.codeSize = body[0] >> 2;
            return parsed.codeOffset + parsed.codeSize <= body.size();
        }
        if (format != 3) return false;
        USHORT flags = 0;
        ULONG codeSize = 0;
        ULONG localSignature = 0;
        if (!ReadU16(body, 0, flags) || !ReadU16(body, 2, parsed.maxStack) ||
            !ReadU32(body, 4, codeSize) || !ReadU32(body, 8, localSignature)) return false;
        const size_t headerSize = static_cast<size_t>((flags >> 12) & 0xF) * 4;
        if (headerSize < 12 || headerSize > body.size()) return false;
        parsed.codeOffset = headerSize;
        parsed.codeSize = codeSize;
        parsed.localSignature = localSignature;
        parsed.moreSections = (flags & 8) != 0;
        parsed.initLocals = (flags & 16) != 0;
        if (parsed.codeOffset + parsed.codeSize > body.size()) return false;
        if (!parsed.moreSections) return true;

        size_t section = Align4(parsed.codeOffset + parsed.codeSize);
        while (section < body.size())
        {
            if (section + 4 > body.size()) return false;
            const BYTE kind = body[section];
            const bool fat = (kind & 0x40) != 0;
            const BYTE sectionKind = kind & 0x3F;
            const ULONG size = fat
                ? static_cast<ULONG>(body[section + 1]) |
                    (static_cast<ULONG>(body[section + 2]) << 8) |
                    (static_cast<ULONG>(body[section + 3]) << 16)
                : body[section + 1];
            if (size < 4 || section + size > body.size() || sectionKind != 1) return false;
            const ULONG clauseSize = fat ? 24u : 12u;
            if ((size - 4) % clauseSize != 0) return false;
            const ULONG count = (size - 4) / clauseSize;
            for (ULONG i = 0; i < count; ++i)
            {
                const size_t at = section + 4 + static_cast<size_t>(i) * clauseSize;
                ExceptionClause clause{};
                if (fat)
                {
                    if (!ReadU32(body, at, clause.flags) ||
                        !ReadU32(body, at + 4, clause.tryOffset) ||
                        !ReadU32(body, at + 8, clause.tryLength) ||
                        !ReadU32(body, at + 12, clause.handlerOffset) ||
                        !ReadU32(body, at + 16, clause.handlerLength) ||
                        !ReadU32(body, at + 20, clause.classOrFilter)) return false;
                }
                else
                {
                    USHORT value = 0;
                    if (!ReadU16(body, at, value)) return false;
                    clause.flags = value;
                    if (!ReadU16(body, at + 2, value)) return false;
                    clause.tryOffset = value;
                    clause.tryLength = body[at + 4];
                    if (!ReadU16(body, at + 5, value)) return false;
                    clause.handlerOffset = value;
                    clause.handlerLength = body[at + 7];
                    if (!ReadU32(body, at + 8, clause.classOrFilter)) return false;
                }
                if (clause.tryOffset + clause.tryLength > parsed.codeSize ||
                    clause.handlerOffset + clause.handlerLength > parsed.codeSize) return false;
                parsed.clauses.push_back(clause);
            }
            section = Align4(section + size);
            if ((kind & 0x80) == 0) break;
        }
        return section == body.size() || section + 3 >= body.size();
    }

    class Bytecode
    {
    public:
        std::vector<BYTE> data;

        void U8(BYTE value) { data.push_back(value); }
        void U32(ULONG value) { PutU32(data, value); }
        void U64(ULONGLONG value)
        {
            U32(static_cast<ULONG>(value));
            U32(static_cast<ULONG>(value >> 32));
        }
        void Token(mdToken value) { PutToken(data, value); }
        void LdcI4(int value)
        {
            if (value >= 0 && value <= 8) U8(static_cast<BYTE>(0x16 + value));
            else if (value >= -128 && value <= 127)
            {
                U8(0x1F);
                U8(static_cast<BYTE>(static_cast<char>(value)));
            }
            else
            {
                U8(0x20);
                U32(static_cast<ULONG>(value));
            }
        }
        void Ldstr(mdString value) { U8(0x72); Token(value); }
        void Call(mdToken value) { U8(0x28); Token(value); }
        void Callvirt(mdToken value) { U8(0x6F); Token(value); }
        void Newobj(mdToken value) { U8(0x73); Token(value); }
        void Newarr(mdToken value) { U8(0x8D); Token(value); }
        void Dup() { U8(0x25); }
        void Ret() { U8(0x2A); }
    };

    bool BuildBody(const std::vector<BYTE> &original, bool detailed, mdString appString,
        mdToken uidGetter, mdToken stringEquality, mdToken planType,
        mdToken intType, mdString titleString, mdString descriptionString,
        const std::vector<int> &plans, const mdToken *planTokens, LONGLONG expiry,
        std::vector<BYTE> &replacement)
    {
        MethodBody parsed;
        if (!ParseMethodBody(original, parsed)) return false;
        const BYTE *originalCode = original.data() + parsed.codeOffset;

        Bytecode prefix;
        prefix.U8(0x02); // ldarg.0
        prefix.Call(uidGetter);
        prefix.Ldstr(appString);
        prefix.Call(stringEquality);
        prefix.U8(0x39); // brfalse (long form)
        const size_t branchOperand = prefix.data.size();
        prefix.U32(0);

        prefix.LdcI4(static_cast<int>(plans.size()));
        if (detailed) prefix.Newarr(planType);
        else prefix.Newarr(intType);

        for (size_t i = 0; i < plans.size(); ++i)
        {
            prefix.Dup();
            prefix.LdcI4(static_cast<int>(i));
            if (detailed)
            {
                prefix.Newobj(planTokens[0]);
                const mdToken setters[] = {
                    planTokens[1], planTokens[2], planTokens[3], planTokens[4],
                    planTokens[5], planTokens[6], planTokens[7]
                };
                prefix.Dup(); prefix.LdcI4(plans[i]); prefix.Callvirt(setters[0]);
                prefix.Dup(); prefix.LdcI4(3); prefix.Callvirt(setters[1]);
                prefix.Dup();
                prefix.U8(0x21); // ldc.i8
                prefix.U64(static_cast<ULONGLONG>(expiry));
                prefix.Callvirt(setters[2]);
                prefix.Dup(); prefix.Ldstr(titleString); prefix.Callvirt(setters[3]);
                prefix.Dup(); prefix.Ldstr(descriptionString); prefix.Callvirt(setters[4]);
                prefix.Dup();
                prefix.U8(0x23); // ldc.r8
                double price = 0.0;
                ULONGLONG priceBits = 0;
                std::memcpy(&priceBits, &price, sizeof(priceBits));
                prefix.U64(priceBits);
                prefix.Callvirt(setters[5]);
                prefix.Dup(); prefix.LdcI4(1); prefix.Callvirt(setters[6]);
                prefix.U8(0xA2); // stelem.ref
            }
            else
            {
                prefix.LdcI4(plans[i]);
                prefix.U8(0x9E); // stelem.i4
            }
        }
        prefix.Ret();
        const size_t originalEntry = prefix.data.size();
        const int64_t displacement = static_cast<int64_t>(originalEntry) -
            static_cast<int64_t>(branchOperand + 4);
        if (displacement < (std::numeric_limits<LONG>::min)() || displacement > (std::numeric_limits<LONG>::max)()) return false;
        const ULONG relative = static_cast<ULONG>(static_cast<LONG>(displacement));
        for (size_t i = 0; i < 4; ++i) prefix.data[branchOperand + i] = static_cast<BYTE>(relative >> (8 * i));

        const ULONG codeSize = static_cast<ULONG>(prefix.data.size() + parsed.codeSize);
        if (codeSize > 0x00FFFFFFu) return false;
        std::vector<BYTE> body;
        const USHORT flags = static_cast<USHORT>(0x3003 | (parsed.initLocals ? 0x10 : 0) |
            (parsed.clauses.empty() ? 0 : 0x8));
        PutU16(body, flags);
        PutU16(body, static_cast<USHORT>(std::max<USHORT>(8, parsed.maxStack)));
        PutU32(body, codeSize);
        PutU32(body, parsed.localSignature);
        body.insert(body.end(), prefix.data.begin(), prefix.data.end());
        body.insert(body.end(), originalCode, originalCode + parsed.codeSize);

        if (!parsed.clauses.empty())
        {
            while (body.size() % 4 != 0) body.push_back(0);
            const ULONG sectionSize = 4u + static_cast<ULONG>(parsed.clauses.size()) * 24u;
            body.push_back(0x41); // fat exception section, EH table
            body.push_back(static_cast<BYTE>(sectionSize));
            body.push_back(static_cast<BYTE>(sectionSize >> 8));
            body.push_back(static_cast<BYTE>(sectionSize >> 16));
            for (const auto &clause : parsed.clauses)
            {
                PutU32(body, clause.flags);
                PutU32(body, clause.tryOffset + static_cast<ULONG>(originalEntry));
                PutU32(body, clause.tryLength);
                PutU32(body, clause.handlerOffset + static_cast<ULONG>(originalEntry));
                PutU32(body, clause.handlerLength);
                // A filter stores an IL offset; a catch stores a metadata token.
                // Only the former moves when the original body is appended.
                const ULONG classOrFilter = (clause.flags & 0x1u) != 0
                    ? clause.classOrFilter + static_cast<ULONG>(originalEntry)
                    : clause.classOrFilter;
                PutU32(body, classOrFilter);
            }
        }
        replacement.swap(body);
        return true;
    }

    bool ValidateMember(IMetaDataImport *import, mdToken token, const wchar_t *expected)
    {
        wchar_t name[128]{};
        ULONG written = 0;
        mdToken parent = 0;
        PCCOR_SIGNATURE signature = nullptr;
        ULONG signatureSize = 0;
        HRESULT hr = import->GetMemberRefProps(token, &parent, name, ARRAYSIZE(name),
            &written, &signature, &signatureSize);
        return SUCCEEDED(hr) && wcscmp(name, expected) == 0;
    }

    bool ValidateMethod(IMetaDataImport *import, mdMethodDef token, const wchar_t *expected)
    {
        wchar_t name[128]{};
        ULONG written = 0;
        mdTypeDef parent = 0;
        DWORD attributes = 0;
        PCCOR_SIGNATURE signature = nullptr;
        ULONG signatureSize = 0;
        ULONG rva = 0;
        DWORD implFlags = 0;
        HRESULT hr = import->GetMethodProps(token, &parent, name, ARRAYSIZE(name),
            &written, &attributes, &signature, &signatureSize, &rva, &implFlags);
        return SUCCEEDED(hr) && wcscmp(name, expected) == 0 &&
            (attributes & 0x0010) == 0 && signatureSize >= 3 &&
            (signature[0] & 0x0F) != 0x05 && signature[1] == 0 && signature[2] == 0x1D;
    }

    bool FindTypeRef(IMetaDataImport *import, const wchar_t *expected, mdToken &token)
    {
        mdTypeDef typeDef = 0;
        if (SUCCEEDED(import->FindTypeDefByName(expected, 0, &typeDef)))
        {
            token = typeDef;
            return true;
        }
        HCORENUM enumeration = nullptr;
        mdTypeRef refs[64]{};
        ULONG fetched = 0;
        while (SUCCEEDED(import->EnumTypeRefs(&enumeration, refs, ARRAYSIZE(refs), &fetched)) && fetched != 0)
        {
            for (ULONG i = 0; i < fetched; ++i)
            {
                wchar_t name[256]{};
                ULONG written = 0;
                mdToken scope = 0;
                if (SUCCEEDED(import->GetTypeRefProps(refs[i], &scope, name, ARRAYSIZE(name), &written)) &&
                    wcscmp(name, expected) == 0)
                {
                    import->CloseEnum(enumeration);
                    token = refs[i];
                    return true;
                }
            }
        }
        if (enumeration) import->CloseEnum(enumeration);
        return false;
    }

    bool FindTypeRefScope(IMetaDataImport *import, const wchar_t *expected, mdToken &scope)
    {
        HCORENUM enumeration = nullptr;
        mdTypeRef refs[64]{};
        ULONG fetched = 0;
        while (SUCCEEDED(import->EnumTypeRefs(&enumeration, refs, ARRAYSIZE(refs), &fetched)) && fetched != 0)
        {
            for (ULONG i = 0; i < fetched; ++i)
            {
                wchar_t name[256]{};
                ULONG written = 0;
                mdToken actualScope = 0;
                if (SUCCEEDED(import->GetTypeRefProps(refs[i], &actualScope, name, ARRAYSIZE(name), &written)) &&
                    wcscmp(name, expected) == 0)
                {
                    import->CloseEnum(enumeration);
                    scope = actualScope;
                    return true;
                }
            }
        }
        if (enumeration) import->CloseEnum(enumeration);
        return false;
    }

    bool FindMemberRefByName(IMetaDataImport *import, mdToken parent, const wchar_t *expected, mdToken &token)
    {
        auto searchParent = [&](mdToken candidateParent) -> bool
        {
            HCORENUM enumeration = nullptr;
            mdMemberRef refs[64]{};
            ULONG fetched = 0;
            while (SUCCEEDED(import->EnumMemberRefs(&enumeration, candidateParent, refs, ARRAYSIZE(refs), &fetched)) && fetched != 0)
            {
                for (ULONG i = 0; i < fetched; ++i)
                {
                    mdToken actualParent = 0;
                    wchar_t name[256]{};
                    ULONG written = 0;
                    PCCOR_SIGNATURE signature = nullptr;
                    ULONG signatureSize = 0;
                    if (SUCCEEDED(import->GetMemberRefProps(refs[i], &actualParent, name, ARRAYSIZE(name),
                        &written, &signature, &signatureSize)) && wcscmp(name, expected) == 0 &&
                        actualParent == candidateParent)
                    {
                        import->CloseEnum(enumeration);
                        token = refs[i];
                        return true;
                    }
                }
            }
            if (enumeration) import->CloseEnum(enumeration);
            return false;
        };

        if (parent != 0) return searchParent(parent);

        HCORENUM typeEnumeration = nullptr;
        mdTypeRef typeRefs[64]{};
        ULONG typeFetched = 0;
        while (SUCCEEDED(import->EnumTypeRefs(&typeEnumeration, typeRefs, ARRAYSIZE(typeRefs), &typeFetched)) && typeFetched != 0)
        {
            for (ULONG t = 0; t < typeFetched; ++t)
                if (searchParent(typeRefs[t]))
                {
                    import->CloseEnum(typeEnumeration);
                    return true;
                }
        }
        if (typeEnumeration) import->CloseEnum(typeEnumeration);

        HCORENUM defEnumeration = nullptr;
        mdTypeDef typeDefs[64]{};
        ULONG defFetched = 0;
        while (SUCCEEDED(import->EnumTypeDefs(&defEnumeration, typeDefs, ARRAYSIZE(typeDefs), &defFetched)) && defFetched != 0)
        {
            for (ULONG d = 0; d < defFetched; ++d)
                if (searchParent(typeDefs[d]))
                {
                    import->CloseEnum(defEnumeration);
                    return true;
                }
        }
        if (defEnumeration) import->CloseEnum(defEnumeration);
        return false;
    }

    bool FindMethodByName(IMetaDataImport *import, mdTypeDef parent, const wchar_t *expected, mdToken &token)
    {
        HCORENUM enumeration = nullptr;
        mdMethodDef methods[64]{};
        ULONG fetched = 0;
        while (SUCCEEDED(import->EnumMethods(&enumeration, parent, methods, ARRAYSIZE(methods), &fetched)) && fetched != 0)
        {
            for (ULONG i = 0; i < fetched; ++i)
            {
                wchar_t name[256]{};
                ULONG written = 0;
                mdTypeDef actualParent = 0;
                DWORD attributes = 0;
                PCCOR_SIGNATURE signature = nullptr;
                ULONG signatureSize = 0;
                ULONG rva = 0;
                DWORD implFlags = 0;
                if (SUCCEEDED(import->GetMethodProps(methods[i], &actualParent, name, ARRAYSIZE(name),
                    &written, &attributes, &signature, &signatureSize, &rva, &implFlags)) &&
                    actualParent == parent && wcscmp(name, expected) == 0)
                {
                    import->CloseEnum(enumeration);
                    token = methods[i];
                    return true;
                }
            }
        }
        if (enumeration) import->CloseEnum(enumeration);
        return false;
    }

    bool ValidateUidGetter(IMetaDataImport *import, mdMethodDef token)
    {
        wchar_t name[128]{};
        ULONG written = 0;
        mdTypeDef parent = 0;
        DWORD attributes = 0;
        PCCOR_SIGNATURE signature = nullptr;
        ULONG signatureSize = 0;
        ULONG rva = 0;
        DWORD implFlags = 0;
        HRESULT hr = import->GetMethodProps(token, &parent, name, ARRAYSIZE(name),
            &written, &attributes, &signature, &signatureSize, &rva, &implFlags);
        return SUCCEEDED(hr) && wcscmp(name, L"get_UID") == 0 &&
            (attributes & 0x0010) == 0 && signatureSize >= 3 &&
            (signature[0] & 0x0F) != 0x05 && signature[1] == 0 && signature[2] == 0x0E;
    }

    bool FindUidGetter(IMetaDataImport *import, mdMethodDef &token, bool flexible)
    {
        HCORENUM types = nullptr;
        mdTypeDef typeTokens[64]{};
        ULONG typeCount = 0;
        while (SUCCEEDED(import->EnumTypeDefs(&types, typeTokens, ARRAYSIZE(typeTokens), &typeCount)) && typeCount != 0)
        {
            for (ULONG t = 0; t < typeCount; ++t)
            {
                HCORENUM methods = nullptr;
                mdMethodDef methodTokens[64]{};
                ULONG methodCount = 0;
                while (SUCCEEDED(import->EnumMethods(&methods, typeTokens[t], methodTokens, ARRAYSIZE(methodTokens), &methodCount)) && methodCount != 0)
                {
                    for (ULONG m = 0; m < methodCount; ++m)
                    {
                        if ((!flexible && methodTokens[m] == kUidGetter || flexible) &&
                            ValidateUidGetter(import, methodTokens[m]))
                        {
                            import->CloseEnum(methods);
                            import->CloseEnum(types);
                            token = methodTokens[m];
                            return true;
                        }
                    }
                }
                if (methods) import->CloseEnum(methods);
            }
        }
        if (types) import->CloseEnum(types);
        return false;
    }

    struct AdapterTokens
    {
        mdMethodDef detailed = 0;
        mdMethodDef ids = 0;
        mdMethodDef uidGetter = 0;
        mdToken planType = 0;
        mdToken intType = 0;
        mdToken stringEquality = 0;
        mdToken plan[8]{};
    };

    bool ResolveReviewedTokens(IMetaDataImport *import, AdapterTokens &tokens, bool flexible)
    {
        mdTypeDef owner = 0;
        if (FAILED(import->FindTypeDefByName(kTargetType, 0, &owner)))
        {
            Log(L"shape: target type not found");
            return false;
        }
        HCORENUM methods = nullptr;
        mdMethodDef candidates[64]{};
        ULONG fetched = 0;
        while (SUCCEEDED(import->EnumMethods(&methods, owner, candidates, ARRAYSIZE(candidates), &fetched)) && fetched != 0)
        {
            for (ULONG i = 0; i < fetched; ++i)
            {
                if (ValidateMethod(import, candidates[i], L"GetExtensionSubscriptions") &&
                    (flexible || candidates[i] == kDetailedMethod)) tokens.detailed = candidates[i];
                if (ValidateMethod(import, candidates[i], L"GetExtensionSubscriptionsIds") &&
                    (flexible || candidates[i] == kIdsMethod)) tokens.ids = candidates[i];
            }
        }
        if (methods) import->CloseEnum(methods);
        if ((!flexible && (tokens.detailed != kDetailedMethod || tokens.ids != kIdsMethod)) ||
            (flexible && (tokens.detailed == 0 || tokens.ids == 0)))
        {
            Log(L"shape: subscription methods not found");
            return false;
        }
        if (!FindUidGetter(import, tokens.uidGetter, flexible))
        {
            Log(L"shape: UID getter not found");
            return false;
        }
        if (!FindTypeRef(import, L"ODKv2API.DetailedActivePlan", tokens.planType))
        {
            Log(L"shape: detailed plan TypeRef not found");
            return false;
        }
        FindTypeRef(import, L"System.Int32", tokens.intType);

        if (flexible)
        {
            if (!FindMemberRefByName(import, 0, L"op_Equality", tokens.stringEquality))
            {
                Log(L"shape: string equality MemberRef not found");
                return false;
            }
        }
        else
        {
            tokens.stringEquality = kStringEquality;
            if (!ValidateMember(import, tokens.stringEquality, L"op_Equality")) return false;
        }
        const mdToken expected[] = {
            kPlanConstructor, kPlanIdSetter, kStateSetter, kExpirySetter,
            kTitleSetter, kDescriptionSetter, kPriceSetter, kPeriodSetter
        };
        const wchar_t *names[] = {
            L".ctor", L"set_PlanId", L"set_State", L"set_ExpiryDate",
            L"set_Title", L"set_Description", L"set_Price", L"set_PeriodMonths"
        };
        for (size_t i = 0; i < ARRAYSIZE(expected); ++i)
        {
            if (flexible)
            {
                if (!FindMemberRefByName(import, tokens.planType, names[i], tokens.plan[i]) &&
                    ((tokens.planType & 0xFF000000u) != 0x02000000u ||
                        !FindMethodByName(import, static_cast<mdTypeDef>(tokens.planType), names[i], tokens.plan[i])))
                {
                    Log(std::wstring(L"shape: plan MemberRef not found: ") + names[i]);
                    return false;
                }
            }
            else
            {
                if (!ValidateMember(import, expected[i], names[i])) return false;
                tokens.plan[i] = expected[i];
            }
        }
        return true;
    }

    bool GetBody(ICorProfilerInfo *info, ModuleID module, mdMethodDef method, std::vector<BYTE> &body)
    {
        LPCBYTE header = nullptr;
        ULONG size = 0;
        HRESULT hr = info->GetILFunctionBody(module, method, &header, &size);
        if (FAILED(hr) || header == nullptr || size == 0) return false;
        body.assign(header, header + size);
        return true;
    }

    bool InstallBody(ICorProfilerInfo *info, ModuleID module, mdMethodDef method,
        const std::vector<BYTE> &body)
    {
        IMethodMalloc *allocator = nullptr;
        HRESULT hr = info->GetILFunctionBodyAllocator(module, &allocator);
        if (FAILED(hr) || allocator == nullptr) return false;
        BYTE *destination = reinterpret_cast<BYTE *>(allocator->Alloc(static_cast<ULONG>(body.size())));
        if (destination == nullptr)
        {
            allocator->Release();
            return false;
        }
        CopyMemory(destination, body.data(), body.size());
        hr = info->SetILFunctionBody(module, method, destination);
        allocator->Release();
        return SUCCEEDED(hr);
    }

    class Profiler final : public ICorProfilerCallback2
    {
        enum class ModuleState
        {
            InFlight,
            Completed,
            Rejected
        };

        LONG refCount_ = 1;
        ICorProfilerInfo *info_ = nullptr;
        std::map<ModuleID, ModuleState> moduleStates_;
        CRITICAL_SECTION lock_{};
        std::wstring mode_;
        std::vector<int> plans_;
        std::wstring appId_;
        bool testMode_ = false;
        bool active_ = false;
        bool instrument_ = false;
        bool logJitDetails_ = false;
        DWORD requestedMask_ = 0;
        LONG jitCallbacks_ = 0;
        LONG jitFunctionInfoFailures_ = 0;
        LONG jitModuleInfoFailures_ = 0;
        LONG coreJitCallbacks_ = 0;
        LONG diagnosticJitLogs_ = 0;

        bool BeginModuleAttempt(ModuleID module)
        {
            EnterCriticalSection(&lock_);
            const bool present = moduleStates_.find(module) != moduleStates_.end();
            if (!present) moduleStates_[module] = ModuleState::InFlight;
            LeaveCriticalSection(&lock_);
            return !present;
        }

        void FinishModuleAttempt(ModuleID module, bool completed, bool rejected)
        {
            EnterCriticalSection(&lock_);
            if (completed) moduleStates_[module] = ModuleState::Completed;
            else if (rejected) moduleStates_[module] = ModuleState::Rejected;
            else moduleStates_.erase(module);
            LeaveCriticalSection(&lock_);
        }

        void ClearProfilerEnvironment()
        {
            const wchar_t *names[] = {
                L"COR_ENABLE_PROFILING", L"COR_PROFILER", L"COR_PROFILER_PATH",
                L"COR_PROFILER_PATH_32", L"COR_PROFILER_PATH_64",
                L"COMPLUS_ProfAPI_ProfilerCompatibilitySetting"
            };
            for (const wchar_t *name : names) SetEnvironmentVariableW(name, nullptr);
        }

        bool IsAuthorizedProcess() const
        {
            const std::wstring expected = Lower(Env(L"OVERWOLF_PATCHER_TARGET_PROCESS"));
            return !expected.empty() && Lower(CurrentProcessImage()) == expected;
        }

        void LogProcessIdentity(const wchar_t *phase)
        {
            Log(std::wstring(phase) + L" pid=" + std::to_wstring(GetCurrentProcessId()) +
                L" ppid=" + std::to_wstring(ParentProcessId()) +
                L" image=" + CurrentProcessImage() + L" clr=" + LoadedClrPath());
        }

        void LogJitDiagnostic(const std::wstring &message)
        {
            if (!logJitDetails_) return;
            const LONG count = InterlockedIncrement(&diagnosticJitLogs_);
            if (count <= 96) Log(message);
        }

        bool ParseConfiguration()
        {
            mode_ = Lower(Env(L"OVERWOLF_PATCHER_PROFILER_MODE"));
            if (mode_.empty()) mode_ = L"neutral";
            if (mode_ != L"bootstrap" && mode_ != L"observe" && mode_ != L"flags" &&
                mode_ != L"neutral" && mode_ != L"premium")
            {
                Log(L"unsupported profiler mode: " + mode_);
                return false;
            }
            instrument_ = mode_ == L"neutral" || mode_ == L"premium";
            logJitDetails_ = Lower(Env(L"OVERWOLF_PATCHER_PROFILER_VERBOSE")) == L"1" ||
                mode_ == L"observe" || mode_ == L"flags";
            if (mode_ != L"premium") return true;
            appId_ = Env(L"OVERWOLF_PATCHER_APP");
            if (appId_.size() != 40) return false;
            for (wchar_t value : appId_)
                if (value < L'a' || value > L'p') return false;
            const std::wstring planText = Env(L"OVERWOLF_PATCHER_PLANS");
            size_t start = 0;
            while (start < planText.size())
            {
                size_t end = planText.find(L',', start);
                if (end == std::wstring::npos) end = planText.size();
                const std::wstring item = planText.substr(start, end - start);
                wchar_t *stop = nullptr;
                long value = wcstol(item.c_str(), &stop, 10);
                if (stop == nullptr || *stop != L'\0' || value <= 0 || value > 0x7FFFFFFF) return false;
                if (std::find(plans_.begin(), plans_.end(), static_cast<int>(value)) == plans_.end()) plans_.push_back(static_cast<int>(value));
                start = end + 1;
            }
            return !plans_.empty() && plans_.size() <= 32;
        }

        bool ValidateModuleIdentity(const std::wstring &path, IMetaDataImport *import)
        {
            if (testMode_)
            {
                Log(L"test mode: accepting fixture module identity");
                return true;
            }
            const std::wstring expected = Lower(Env(L"OVERWOLF_PATCHER_EXPECTED_CORE_SHA256")).empty()
                ? std::wstring(kReviewedCoreHash) : Lower(Env(L"OVERWOLF_PATCHER_EXPECTED_CORE_SHA256"));
            const std::wstring actual = Lower(HashFile(path));
            if (actual.empty() || actual != Lower(expected))
            {
                Log(L"refusing unknown Core hash: " + actual);
                return false;
            }
            GUID mvid{};
            ULONG written = 0;
            wchar_t scopeName[256]{};
            if (FAILED(import->GetScopeProps(scopeName, ARRAYSIZE(scopeName), &written, &mvid)) || !IsEqualGUID(mvid, kExpectedMvid))
            {
                Log(L"refusing unknown Core MVID");
                return false;
            }
            return true;
        }

        void InstrumentModule(ModuleID module, const std::wstring &path, mdToken triggerToken)
        {
            if (Lower(BaseName(path)) != Lower(kCoreName)) return;
            if (!BeginModuleAttempt(module)) return;
            struct AttemptGuard
            {
                Profiler *owner;
                ModuleID module;
                bool completed = false;
                bool rejected = false;
                ~AttemptGuard() { owner->FinishModuleAttempt(module, completed, rejected); }
            } guard{ this, module };
            Log(L"Core module observed trigger=" + HexValue(triggerToken) + L": " + path);

            IUnknown *metadataUnknown = nullptr;
            HRESULT hr = info_->GetModuleMetaData(module, 0x1,
                __uuidof(IMetaDataImport), &metadataUnknown);
            if (FAILED(hr) || metadataUnknown == nullptr)
            {
                Log(L"metadata unavailable: " + HResult(hr));
                return;
            }
            IMetaDataImport *import = reinterpret_cast<IMetaDataImport *>(metadataUnknown);
            if (!ValidateModuleIdentity(path, import))
            {
                guard.rejected = true;
                import->Release();
                return;
            }

            AdapterTokens tokens{};
            if (!ResolveReviewedTokens(import, tokens, testMode_))
            {
                Log(L"reviewed Core metadata shape was not found");
                guard.rejected = true;
                import->Release();
                return;
            }

            if (testMode_ && triggerToken != tokens.detailed && triggerToken != tokens.ids)
            {
                import->Release();
                return;
            }

            std::vector<BYTE> detailedOriginal;
            std::vector<BYTE> idsOriginal;
            if (!GetBody(info_, module, tokens.detailed, detailedOriginal) ||
                !GetBody(info_, module, tokens.ids, idsOriginal))
            {
                Log(L"target method body unavailable before JIT");
                import->Release();
                return;
            }

            std::vector<BYTE> detailedReplacement = detailedOriginal;
            std::vector<BYTE> idsReplacement = idsOriginal;
            if (mode_ == L"premium")
            {
                ICorProfilerMetadataEmit *emit = nullptr;
                hr = metadataUnknown->QueryInterface(__uuidof(ICorProfilerMetadataEmit), reinterpret_cast<void **>(&emit));
                if (FAILED(hr) || emit == nullptr)
                {
                    Log(L"metadata emit unavailable: " + HResult(hr));
                    import->Release();
                    return;
                }
                mdString appString = 0;
                hr = emit->DefineUserString(appId_.c_str(), static_cast<ULONG>(appId_.size()), &appString);
                if (FAILED(hr) || appString == 0)
                {
                    Log(L"DefineUserString failed: " + HResult(hr));
                    emit->Release();
                    import->Release();
                    return;
                }
                if (tokens.intType == 0)
                {
                    mdToken systemScope = 0;
                    if (!FindTypeRefScope(import, L"System.String", systemScope) ||
                        FAILED(emit->DefineTypeRefByName(systemScope, L"System.Int32", &tokens.intType)))
                    {
                        Log(L"System.Int32 TypeRef could not be created");
                        emit->Release();
                        import->Release();
                        return;
                    }
                }
                mdString title = 0;
                mdString description = 0;
                hr = emit->DefineUserString(L"Local premium test", 18, &title);
                if (SUCCEEDED(hr)) hr = emit->DefineUserString(
                    L"Seven-day local API fixture; no server entitlement", 50, &description);
                if (FAILED(hr) || title == 0 || description == 0)
                {
                    Log(L"display user-string preflight failed: " + HResult(hr));
                    emit->Release();
                    import->Release();
                    return;
                }
                const LONGLONG expiry = std::chrono::duration_cast<std::chrono::milliseconds>(
                    std::chrono::system_clock::now().time_since_epoch()).count() +
                    7LL * 24LL * 60LL * 60LL * 1000LL;
                const bool detailedBuilt = BuildBody(detailedOriginal, true, appString,
                    tokens.uidGetter, tokens.stringEquality, tokens.planType, tokens.intType,
                    title, description, plans_, tokens.plan, expiry, detailedReplacement);
                const bool idsBuilt = BuildBody(idsOriginal, false, appString,
                    tokens.uidGetter, tokens.stringEquality, tokens.planType, tokens.intType,
                    title, description, plans_, tokens.plan, expiry, idsReplacement);
                if (!detailedBuilt || !idsBuilt)
                {
                    Log(L"premium IL preflight failed; no target body was activated");
                    guard.rejected = true;
                    emit->Release();
                    import->Release();
                    return;
                }
                const bool detailedOkay = InstallBody(info_, module, tokens.detailed, detailedReplacement);
                const bool idsOkay = detailedOkay && InstallBody(info_, module, tokens.ids, idsReplacement);
                bool rollbackOkay = true;
                if (detailedOkay && !idsOkay)
                    rollbackOkay = InstallBody(info_, module, tokens.detailed, detailedOriginal);
                Log(std::wstring(L"premium SetILFunctionBody detailed=") + (detailedOkay ? L"ok" : L"failed") +
                    L" ids=" + (idsOkay ? L"ok" : L"failed") +
                    L" rollback=" + (rollbackOkay ? L"ok" : L"failed"));
                if (detailedOkay && idsOkay) guard.completed = true;
                else if (detailedOkay && !rollbackOkay) guard.rejected = true;
                emit->Release();
                import->Release();
                return;
            }

            const bool detailedOkay = InstallBody(info_, module, tokens.detailed, detailedReplacement);
            const bool idsOkay = detailedOkay && InstallBody(info_, module, tokens.ids, idsReplacement);
            bool rollbackOkay = true;
            if (detailedOkay && !idsOkay)
                rollbackOkay = InstallBody(info_, module, tokens.detailed, detailedOriginal);
            Log(std::wstring(L"neutral SetILFunctionBody detailed=") + (detailedOkay ? L"ok" : L"failed") +
                L" ids=" + (idsOkay ? L"ok" : L"failed") +
                L" rollback=" + (rollbackOkay ? L"ok" : L"failed"));
            if (detailedOkay && idsOkay) guard.completed = true;
            else if (detailedOkay && !rollbackOkay) guard.rejected = true;
            import->Release();
        }

    public:
        Profiler()
        {
            InterlockedIncrement(&g_objectCount);
            InitializeCriticalSection(&lock_);
        }

        ~Profiler()
        {
            if (info_) info_->Release();
            DeleteCriticalSection(&lock_);
            InterlockedDecrement(&g_objectCount);
        }

        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void **object) override
        {
            if (object == nullptr) return E_POINTER;
            *object = nullptr;
            if (riid == IID_IUnknown || riid == __uuidof(ICorProfilerCallback) || riid == __uuidof(ICorProfilerCallback2))
            {
                *object = static_cast<ICorProfilerCallback2 *>(this);
                AddRef();
                return S_OK;
            }
            return E_NOINTERFACE;
        }

        ULONG STDMETHODCALLTYPE AddRef() override { return static_cast<ULONG>(InterlockedIncrement(&refCount_)); }
        ULONG STDMETHODCALLTYPE Release() override
        {
            ULONG result = static_cast<ULONG>(InterlockedDecrement(&refCount_));
            if (result == 0) delete this;
            return result;
        }

        HRESULT STDMETHODCALLTYPE Initialize(IUnknown *unknown) override
        {
            if (!unknown || FAILED(unknown->QueryInterface(__uuidof(ICorProfilerInfo), reinterpret_cast<void **>(&info_)))) return E_FAIL;
            testMode_ = Lower(Env(L"OVERWOLF_PATCHER_PROFILER_TEST")) == L"1";
            if (!ParseConfiguration()) return E_INVALIDARG;
            LogProcessIdentity(L"profiler initialize");
            if (!testMode_ && !IsAuthorizedProcess())
            {
                Log(L"profiler inactive: current executable is not the authorized target; profiling will not propagate");
                ClearProfilerEnvironment();
                return S_OK;
            }

            requestedMask_ = 0;
            if (mode_ != L"bootstrap")
                requestedMask_ = COR_PRF_MONITOR_MODULE_LOADS | COR_PRF_MONITOR_JIT_COMPILATION;
            if (mode_ == L"flags" || mode_ == L"neutral" || mode_ == L"premium")
                requestedMask_ |= COR_PRF_DISABLE_INLINING | COR_PRF_DISABLE_ALL_NGEN_IMAGES;

            const HRESULT setHr = info_->SetEventMask(requestedMask_);
            DWORD effectiveMask = 0;
            const HRESULT getHr = info_->GetEventMask(&effectiveMask);
            active_ = SUCCEEDED(setHr);
            Log(std::wstring(L"profiler initialized mode=") + mode_ +
                L" requestedMask=" + HexValue(requestedMask_) +
                L" setEventMask=" + HResult(setHr) +
                L" getEventMask=" + HResult(getHr) +
                L" effectiveMask=" + HexValue(effectiveMask));
            ClearProfilerEnvironment();
            return active_ ? S_OK : setHr;
        }

        HRESULT STDMETHODCALLTYPE Shutdown() override
        {
            Log(std::wstring(L"profiler shutdown pid=") + std::to_wstring(GetCurrentProcessId()) +
                L" jit=" + std::to_wstring(jitCallbacks_) +
                L" getFunctionInfoFailures=" + std::to_wstring(jitFunctionInfoFailures_) +
                L" getModuleInfoFailures=" + std::to_wstring(jitModuleInfoFailures_) +
                L" coreJit=" + std::to_wstring(coreJitCallbacks_));
            if (info_)
            {
                info_->Release();
                info_ = nullptr;
            }
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE ModuleLoadFinished(ModuleID module, HRESULT status) override
        {
            if (active_ && info_)
            {
                LPCBYTE base = nullptr;
                WCHAR path[4096]{};
                ULONG written = 0;
                AssemblyID assembly = 0;
                HRESULT hr = info_->GetModuleInfo(module, &base, ARRAYSIZE(path), &written, path, &assembly);
                if (SUCCEEDED(hr) && IsTrackedModule(path))
                    Log(std::wstring(L"module load finished name=") + BaseName(path) +
                        L" status=" + HResult(status) + L" module=" + HexValue(module));
                else if (FAILED(status) && SUCCEEDED(hr))
                    Log(std::wstring(L"tracked module load failed name=") + BaseName(path) +
                        L" status=" + HResult(status));
            }
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE JITInlining(FunctionID, FunctionID, BOOL *shouldInline) override
        {
            if (shouldInline && (mode_ == L"flags" || mode_ == L"neutral" || mode_ == L"premium"))
                *shouldInline = FALSE;
            return S_OK;
        }

#define PROFILER_UNUSED0(name) HRESULT STDMETHODCALLTYPE name() override { return S_OK; }
#define PROFILER_UNUSED1(name, a) HRESULT STDMETHODCALLTYPE name(a) override { return S_OK; }
#define PROFILER_UNUSED2(name, a, b) HRESULT STDMETHODCALLTYPE name(a, b) override { return S_OK; }
#define PROFILER_UNUSED3(name, a, b, c) HRESULT STDMETHODCALLTYPE name(a, b, c) override { return S_OK; }
#define PROFILER_UNUSED4(name, a, b, c, d) HRESULT STDMETHODCALLTYPE name(a, b, c, d) override { return S_OK; }
#define PROFILER_UNUSED5(name, a, b, c, d, e) HRESULT STDMETHODCALLTYPE name(a, b, c, d, e) override { return S_OK; }

        PROFILER_UNUSED1(AppDomainCreationStarted, AppDomainID)
        PROFILER_UNUSED2(AppDomainCreationFinished, AppDomainID, HRESULT)
        PROFILER_UNUSED1(AppDomainShutdownStarted, AppDomainID)
        PROFILER_UNUSED2(AppDomainShutdownFinished, AppDomainID, HRESULT)
        PROFILER_UNUSED1(AssemblyLoadStarted, AssemblyID)
        PROFILER_UNUSED2(AssemblyLoadFinished, AssemblyID, HRESULT)
        PROFILER_UNUSED1(AssemblyUnloadStarted, AssemblyID)
        PROFILER_UNUSED2(AssemblyUnloadFinished, AssemblyID, HRESULT)
        PROFILER_UNUSED1(ModuleLoadStarted, ModuleID)
        PROFILER_UNUSED1(ModuleUnloadStarted, ModuleID)
        PROFILER_UNUSED2(ModuleUnloadFinished, ModuleID, HRESULT)
        PROFILER_UNUSED2(ModuleAttachedToAssembly, ModuleID, AssemblyID)
        PROFILER_UNUSED1(ClassLoadStarted, ClassID)
        PROFILER_UNUSED2(ClassLoadFinished, ClassID, HRESULT)
        PROFILER_UNUSED1(ClassUnloadStarted, ClassID)
        PROFILER_UNUSED2(ClassUnloadFinished, ClassID, HRESULT)
        PROFILER_UNUSED1(FunctionUnloadStarted, FunctionID)
        HRESULT STDMETHODCALLTYPE JITCompilationStarted(FunctionID function, BOOL) override
        {
            if (!active_ || !info_) return S_OK;
            InterlockedIncrement(&jitCallbacks_);
            ClassID klass = 0;
            ModuleID module = 0;
            mdToken token = 0;
            const HRESULT functionHr = info_->GetFunctionInfo(function, &klass, &module, &token);
            if (FAILED(functionHr))
            {
                InterlockedIncrement(&jitFunctionInfoFailures_);
                LogJitDiagnostic(L"JIT callback function=" + HexValue(function) +
                    L" GetFunctionInfo=" + HResult(functionHr));
                return S_OK;
            }

            LPCBYTE base = nullptr;
            WCHAR path[4096]{};
            ULONG written = 0;
            AssemblyID assembly = 0;
            const HRESULT moduleHr = info_->GetModuleInfo(module, &base, ARRAYSIZE(path), &written, path, &assembly);
            if (FAILED(moduleHr))
            {
                InterlockedIncrement(&jitModuleInfoFailures_);
                LogJitDiagnostic(L"JIT callback function=" + HexValue(function) +
                    L" module=" + HexValue(module) + L" GetModuleInfo=" + HResult(moduleHr));
                return S_OK;
            }

            const std::wstring modulePath(path);
            if (!IsTrackedModule(modulePath)) return S_OK;
            if (Lower(BaseName(modulePath)) == Lower(kCoreName))
            {
                InterlockedIncrement(&coreJitCallbacks_);
                const bool target = token == kDetailedMethod || token == kIdsMethod;
                if (target)
                    Log(L"JIT target started function=" + HexValue(function) +
                        L" token=" + HexValue(token));
                else
                    LogJitDiagnostic(L"Core JIT started function=" + HexValue(function) +
                        L" token=" + HexValue(token));
                if (instrument_ && (testMode_ || target))
                    InstrumentModule(module, modulePath, token);
            }
            else
                LogJitDiagnostic(L"CEF JIT started function=" + HexValue(function) +
                    L" token=" + HexValue(token));
            return S_OK;
        }
        HRESULT STDMETHODCALLTYPE JITCompilationFinished(FunctionID function, HRESULT status, BOOL) override
        {
            if (!active_ || !info_) return S_OK;
            ClassID klass = 0;
            ModuleID module = 0;
            mdToken token = 0;
            if (FAILED(info_->GetFunctionInfo(function, &klass, &module, &token))) return S_OK;
            LPCBYTE base = nullptr;
            WCHAR path[4096]{};
            ULONG written = 0;
            AssemblyID assembly = 0;
            if (FAILED(info_->GetModuleInfo(module, &base, ARRAYSIZE(path), &written, path, &assembly))) return S_OK;
            const std::wstring modulePath(path);
            if (Lower(BaseName(modulePath)) == Lower(kCoreName) &&
                (logJitDetails_ || token == kDetailedMethod || token == kIdsMethod))
                Log(L"Core JIT finished function=" + HexValue(function) +
                    L" token=" + HexValue(token) + L" status=" + HResult(status));
            return S_OK;
        }
        HRESULT STDMETHODCALLTYPE JITCachedFunctionSearchStarted(FunctionID function, BOOL *) override
        {
            if (!active_ || !info_) return S_OK;
            ClassID klass = 0;
            ModuleID module = 0;
            mdToken token = 0;
            if (FAILED(info_->GetFunctionInfo(function, &klass, &module, &token))) return S_OK;
            LPCBYTE base = nullptr;
            WCHAR path[4096]{};
            ULONG written = 0;
            AssemblyID assembly = 0;
            if (FAILED(info_->GetModuleInfo(module, &base, ARRAYSIZE(path), &written, path, &assembly))) return S_OK;
            if (IsTrackedModule(path))
                LogJitDiagnostic(L"JIT cached search function=" + HexValue(function) +
                    L" module=" + BaseName(path) + L" token=" + HexValue(token));
            return S_OK;
        }
        HRESULT STDMETHODCALLTYPE JITCachedFunctionSearchFinished(FunctionID function, COR_PRF_JIT_CACHE result) override
        {
            if (logJitDetails_ && info_)
            {
                ClassID klass = 0;
                ModuleID module = 0;
                mdToken token = 0;
                if (SUCCEEDED(info_->GetFunctionInfo(function, &klass, &module, &token)))
                    LogJitDiagnostic(L"JIT cached finished function=" + HexValue(function) +
                        L" token=" + HexValue(token) + L" result=" + HexValue(result));
            }
            return S_OK;
        }
        PROFILER_UNUSED1(JITFunctionPitched, FunctionID)
        PROFILER_UNUSED1(ThreadCreated, ThreadID)
        PROFILER_UNUSED1(ThreadDestroyed, ThreadID)
        PROFILER_UNUSED2(ThreadAssignedToOSThread, ThreadID, DWORD)
        PROFILER_UNUSED0(RemotingClientInvocationStarted)
        PROFILER_UNUSED2(RemotingClientSendingMessage, GUID *, BOOL)
        PROFILER_UNUSED2(RemotingClientReceivingReply, GUID *, BOOL)
        PROFILER_UNUSED0(RemotingClientInvocationFinished)
        PROFILER_UNUSED2(RemotingServerReceivingMessage, GUID *, BOOL)
        PROFILER_UNUSED0(RemotingServerInvocationStarted)
        PROFILER_UNUSED0(RemotingServerInvocationReturned)
        PROFILER_UNUSED2(RemotingServerSendingReply, GUID *, BOOL)
        PROFILER_UNUSED2(UnmanagedToManagedTransition, FunctionID, COR_PRF_TRANSITION_REASON)
        PROFILER_UNUSED2(ManagedToUnmanagedTransition, FunctionID, COR_PRF_TRANSITION_REASON)
        PROFILER_UNUSED1(RuntimeSuspendStarted, COR_PRF_SUSPEND_REASON)
        PROFILER_UNUSED0(RuntimeSuspendFinished)
        PROFILER_UNUSED0(RuntimeSuspendAborted)
        PROFILER_UNUSED0(RuntimeResumeStarted)
        PROFILER_UNUSED0(RuntimeResumeFinished)
        PROFILER_UNUSED1(RuntimeThreadSuspended, ThreadID)
        PROFILER_UNUSED1(RuntimeThreadResumed, ThreadID)
        PROFILER_UNUSED4(MovedReferences, ULONG, ObjectID[], ObjectID[], ULONG[])
        PROFILER_UNUSED2(ObjectAllocated, ObjectID, ClassID)
        PROFILER_UNUSED3(ObjectsAllocatedByClass, ULONG, ClassID[], ULONG[])
        PROFILER_UNUSED4(ObjectReferences, ObjectID, ClassID, ULONG, ObjectID[])
        PROFILER_UNUSED2(RootReferences, ULONG, ObjectID[])
        PROFILER_UNUSED1(ExceptionThrown, ObjectID)
        PROFILER_UNUSED1(ExceptionSearchFunctionEnter, FunctionID)
        PROFILER_UNUSED0(ExceptionSearchFunctionLeave)
        PROFILER_UNUSED1(ExceptionSearchFilterEnter, FunctionID)
        PROFILER_UNUSED0(ExceptionSearchFilterLeave)
        PROFILER_UNUSED1(ExceptionSearchCatcherFound, FunctionID)
        PROFILER_UNUSED1(ExceptionOSHandlerEnter, UINT_PTR)
        PROFILER_UNUSED1(ExceptionOSHandlerLeave, UINT_PTR)
        PROFILER_UNUSED1(ExceptionUnwindFunctionEnter, FunctionID)
        PROFILER_UNUSED0(ExceptionUnwindFunctionLeave)
        PROFILER_UNUSED1(ExceptionUnwindFinallyEnter, FunctionID)
        PROFILER_UNUSED0(ExceptionUnwindFinallyLeave)
        PROFILER_UNUSED2(ExceptionCatcherEnter, FunctionID, ObjectID)
        PROFILER_UNUSED0(ExceptionCatcherLeave)
        PROFILER_UNUSED4(COMClassicVTableCreated, ClassID, REFGUID, void *, ULONG)
        PROFILER_UNUSED3(COMClassicVTableDestroyed, ClassID, REFGUID, void *)
        PROFILER_UNUSED0(ExceptionCLRCatcherFound)
        PROFILER_UNUSED0(ExceptionCLRCatcherExecute)
        PROFILER_UNUSED3(ThreadNameChanged, ThreadID, ULONG, WCHAR[])
        PROFILER_UNUSED3(GarbageCollectionStarted, int, BOOL[], COR_PRF_GC_REASON)
        PROFILER_UNUSED3(SurvivingReferences, ULONG, ObjectID[], ULONG[])
        PROFILER_UNUSED0(GarbageCollectionFinished)
        PROFILER_UNUSED2(FinalizeableObjectQueued, DWORD, ObjectID)
        PROFILER_UNUSED5(RootReferences2, ULONG, ObjectID[], COR_PRF_GC_ROOT_KIND[], COR_PRF_GC_ROOT_FLAGS[], UINT_PTR[])
        PROFILER_UNUSED2(HandleCreated, GCHandleID, ObjectID)
        PROFILER_UNUSED1(HandleDestroyed, GCHandleID)

#undef PROFILER_UNUSED0
#undef PROFILER_UNUSED1
#undef PROFILER_UNUSED2
#undef PROFILER_UNUSED3
#undef PROFILER_UNUSED4
#undef PROFILER_UNUSED5
    };

    class ClassFactory final : public IClassFactory
    {
        LONG refCount_ = 1;
    public:
        ClassFactory() { InterlockedIncrement(&g_objectCount); }
        ~ClassFactory() { InterlockedDecrement(&g_objectCount); }
        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void **object) override
        {
            if (!object) return E_POINTER;
            *object = nullptr;
            if (riid == IID_IUnknown || riid == IID_IClassFactory)
            {
                *object = static_cast<IClassFactory *>(this);
                AddRef();
                return S_OK;
            }
            return E_NOINTERFACE;
        }
        ULONG STDMETHODCALLTYPE AddRef() override { return static_cast<ULONG>(InterlockedIncrement(&refCount_)); }
        ULONG STDMETHODCALLTYPE Release() override
        {
            ULONG result = static_cast<ULONG>(InterlockedDecrement(&refCount_));
            if (result == 0) delete this;
            return result;
        }
        HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown *outer, REFIID riid, void **object) override
        {
            if (outer) return CLASS_E_NOAGGREGATION;
            if (!object) return E_POINTER;
            *object = nullptr;
            Profiler *profiler = new (std::nothrow) Profiler();
            if (!profiler) return E_OUTOFMEMORY;
            HRESULT hr = profiler->QueryInterface(riid, object);
            profiler->Release();
            return hr;
        }
        HRESULT STDMETHODCALLTYPE LockServer(BOOL lock) override
        {
            if (lock) InterlockedIncrement(&g_serverLocks);
            else InterlockedDecrement(&g_serverLocks);
            return S_OK;
        }
    };
}

struct __declspec(uuid("4D7C38E9-7C8A-4F5D-9D2C-1D3E7BC9F1A4")) OverwolfPatcherProfilerClsid;

STDAPI DllGetClassObject(REFCLSID clsid,
    REFIID riid, void **object)
{
    if (!IsEqualGUID(clsid, __uuidof(OverwolfPatcherProfilerClsid))) return CLASS_E_CLASSNOTAVAILABLE;
    ClassFactory *factory = new (std::nothrow) ClassFactory();
    if (!factory) return E_OUTOFMEMORY;
    HRESULT hr = factory->QueryInterface(riid, object);
    factory->Release();
    return hr;
}

STDAPI DllCanUnloadNow()
{
    return (g_objectCount == 0 && g_serverLocks == 0) ? S_OK : S_FALSE;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(instance);
        InitializeCriticalSection(&g_logLock);
        g_logLockReady = true;
    }
    else if (reason == DLL_PROCESS_DETACH && g_logLockReady)
    {
        DeleteCriticalSection(&g_logLock);
        g_logLockReady = false;
    }
    return TRUE;
}
