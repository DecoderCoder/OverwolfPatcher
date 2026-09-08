#pragma once

// This project deliberately keeps the native profiler's ABI declarations local.
// The Windows SDK does not ship CorProf.h, while the CLR profiling ABI is part
// of the .NET Framework installation. The declarations below mirror the v2
// interfaces used by this profiler; do not reorder them.

#include <windows.h>
#include <unknwn.h>

typedef void *HCORENUM;
typedef unsigned char COR_SIGNATURE;
typedef COR_SIGNATURE *PCOR_SIGNATURE;
typedef const COR_SIGNATURE *PCCOR_SIGNATURE;
typedef ULONG32 mdToken;
typedef mdToken mdModule;
typedef mdToken mdTypeRef;
typedef mdToken mdTypeDef;
typedef mdToken mdFieldDef;
typedef mdToken mdMethodDef;
typedef mdToken mdParamDef;
typedef mdToken mdInterfaceImpl;
typedef mdToken mdMemberRef;
typedef mdToken mdCustomAttribute;
typedef mdToken mdPermission;
typedef mdToken mdSignature;
typedef mdToken mdEvent;
typedef mdToken mdProperty;
typedef mdToken mdModuleRef;
typedef mdToken mdAssembly;
typedef mdToken mdAssemblyRef;
typedef mdToken mdFile;
typedef mdToken mdExportedType;
typedef mdToken mdManifestResource;
typedef mdToken mdTypeSpec;
typedef mdToken mdGenericParam;
typedef mdToken mdMethodSpec;
typedef mdToken mdGenericParamConstraint;
typedef mdToken mdString;

typedef UINT_PTR ProcessID;
typedef UINT_PTR AssemblyID;
typedef UINT_PTR AppDomainID;
typedef UINT_PTR ModuleID;
typedef UINT_PTR ClassID;
typedef UINT_PTR ThreadID;
typedef UINT_PTR ContextID;
typedef UINT_PTR FunctionID;
typedef UINT_PTR ObjectID;
typedef UINT_PTR GCHandleID;

typedef UINT_PTR COR_PRF_FRAME_INFO;
typedef UINT_PTR COR_PRF_ELT_INFO;
typedef UINT_PTR ReJITID;

typedef enum _COR_PRF_MONITOR
{
    COR_PRF_MONITOR_MODULE_LOADS = 0x00000004,
    COR_PRF_MONITOR_JIT_COMPILATION = 0x00000020,
    COR_PRF_DISABLE_INLINING = 0x00200000,
    COR_PRF_DISABLE_TRANSPARENCY_CHECKS_UNDER_FULL_TRUST = 0x40000000,
    COR_PRF_DISABLE_ALL_NGEN_IMAGES = 0x80000000
} COR_PRF_MONITOR;

typedef ULONG COR_PRF_JIT_CACHE;
typedef ULONG COR_PRF_TRANSITION_REASON;
typedef ULONG COR_PRF_SUSPEND_REASON;
typedef ULONG COR_PRF_GC_REASON;
typedef ULONG COR_PRF_GC_ROOT_KIND;
typedef ULONG COR_PRF_GC_ROOT_FLAGS;

typedef struct _COR_DEBUG_IL_TO_NATIVE_MAP
{
    ULONG32 ilOffset;
    ULONG32 nativeStartOffset;
    ULONG32 nativeEndOffset;
} COR_DEBUG_IL_TO_NATIVE_MAP;

typedef struct _COR_IL_MAP
{
    ULONG32 oldOffset;
    ULONG32 newOffset;
    BOOL fAccurate;
} COR_IL_MAP;

struct __declspec(uuid("7DAC8207-D3AE-4C75-9B67-92801A497D44")) IMetaDataImport : IUnknown
{
    virtual void STDMETHODCALLTYPE CloseEnum(HCORENUM) = 0;
    virtual HRESULT STDMETHODCALLTYPE CountEnum(HCORENUM, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE ResetEnum(HCORENUM, ULONG) = 0;
    virtual HRESULT STDMETHODCALLTYPE EnumTypeDefs(HCORENUM *, mdTypeDef[], ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE EnumInterfaceImpls(HCORENUM *, mdTypeDef, mdInterfaceImpl[], ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE EnumTypeRefs(HCORENUM *, mdTypeRef[], ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE FindTypeDefByName(LPCWSTR, mdToken, mdTypeDef *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetScopeProps(LPWSTR, ULONG, ULONG *, GUID *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetModuleFromScope(mdModule *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetTypeDefProps(mdTypeDef, LPWSTR, ULONG, ULONG *, DWORD *, mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetInterfaceImplProps(mdInterfaceImpl, mdTypeDef *, mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetTypeRefProps(mdTypeRef, mdToken *, LPWSTR, ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE ResolveTypeRef(mdTypeRef, REFIID, IUnknown **, mdTypeDef *) = 0;
    virtual HRESULT STDMETHODCALLTYPE EnumMembers(HCORENUM *, mdTypeDef, mdToken[], ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE EnumMembersWithName(HCORENUM *, mdTypeDef, LPCWSTR, mdToken[], ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE EnumMethods(HCORENUM *, mdTypeDef, mdMethodDef[], ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE EnumMethodsWithName(HCORENUM *, mdTypeDef, LPCWSTR, mdMethodDef[], ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE EnumFields(HCORENUM *, mdTypeDef, mdFieldDef[], ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE EnumFieldsWithName(HCORENUM *, mdTypeDef, LPCWSTR, mdFieldDef[], ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE EnumParams(HCORENUM *, mdMethodDef, mdParamDef[], ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE EnumMemberRefs(HCORENUM *, mdToken, mdMemberRef[], ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE EnumMethodImpls(HCORENUM *, mdTypeDef, mdToken[], mdToken[], ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE EnumPermissionSets(HCORENUM *, mdToken, DWORD, mdPermission[], ULONG, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE FindMember(mdTypeDef, LPCWSTR, PCCOR_SIGNATURE, ULONG, mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE FindMethod(mdTypeDef, LPCWSTR, PCCOR_SIGNATURE, ULONG, mdMethodDef *) = 0;
    virtual HRESULT STDMETHODCALLTYPE FindField(mdTypeDef, LPCWSTR, PCCOR_SIGNATURE, ULONG, mdFieldDef *) = 0;
    virtual HRESULT STDMETHODCALLTYPE FindMemberRef(mdTypeRef, LPCWSTR, PCCOR_SIGNATURE, ULONG, mdMemberRef *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetMethodProps(mdMethodDef, mdTypeDef *, LPWSTR, ULONG, ULONG *, DWORD *, PCCOR_SIGNATURE *, ULONG *, ULONG *, DWORD *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetMemberRefProps(mdMemberRef, mdToken *, LPWSTR, ULONG, ULONG *, PCCOR_SIGNATURE *, ULONG *) = 0;
};

typedef void (STDMETHODCALLTYPE FunctionEnter)(FunctionID functionId);
typedef void (STDMETHODCALLTYPE FunctionLeave)(FunctionID functionId);
typedef void (STDMETHODCALLTYPE FunctionTailcall)(FunctionID functionId);

typedef void (STDMETHODCALLTYPE FunctionEnter2)(FunctionID functionId, UINT_PTR clientData,
    COR_PRF_FRAME_INFO func, void *argumentInfo);
typedef void (STDMETHODCALLTYPE FunctionLeave2)(FunctionID functionId, UINT_PTR clientData,
    COR_PRF_FRAME_INFO func, void *retvalRange);
typedef void (STDMETHODCALLTYPE FunctionTailcall2)(FunctionID functionId, UINT_PTR clientData,
    COR_PRF_FRAME_INFO func);

struct ICorProfilerFunctionControl;
struct ICorProfilerAssemblyReferenceProvider;

struct __declspec(uuid("A0EFB28B-6EE2-4D7B-B983-A75EF7BEEDB8")) IMethodMalloc : IUnknown
{
    virtual PVOID STDMETHODCALLTYPE Alloc(ULONG cb) = 0;
};

struct __declspec(uuid("28B5557D-3F3F-48B4-90B2-5F9EEA2F6C48")) ICorProfilerInfo : IUnknown
{
    virtual HRESULT STDMETHODCALLTYPE GetClassFromObject(ObjectID, ClassID *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetClassFromToken(ModuleID, mdTypeDef, ClassID *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetCodeInfo(FunctionID, LPCBYTE *, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetEventMask(DWORD *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetFunctionFromIP(LPCBYTE, FunctionID *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetFunctionFromToken(ModuleID, mdToken, FunctionID *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetHandleFromThread(ThreadID, HANDLE *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetObjectSize(ObjectID, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE IsArrayClass(ClassID, ULONG *, ClassID *, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetThreadInfo(ThreadID, DWORD *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetCurrentThreadID(ThreadID *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetClassIDInfo(ClassID, ModuleID *, mdTypeDef *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetFunctionInfo(FunctionID, ClassID *, ModuleID *, mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetEventMask(DWORD) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks(FunctionEnter *, FunctionLeave *, FunctionTailcall *) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetFunctionIDMapper(void *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetTokenAndMetaDataFromFunction(FunctionID, REFIID, IUnknown **, mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetModuleInfo(ModuleID, LPCBYTE *, ULONG, ULONG *, WCHAR[], AssemblyID *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetModuleMetaData(ModuleID, DWORD, REFIID, IUnknown **) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetILFunctionBody(ModuleID, mdMethodDef, LPCBYTE *, ULONG *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetILFunctionBodyAllocator(ModuleID, IMethodMalloc **) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetILFunctionBody(ModuleID, mdMethodDef, LPCBYTE) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetAppDomainInfo(AppDomainID, ULONG, ULONG *, WCHAR[], ProcessID *) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetAssemblyInfo(AssemblyID, ULONG, ULONG *, WCHAR[], AppDomainID *, ModuleID *) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetFunctionReJIT(FunctionID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ForceGC() = 0;
    virtual HRESULT STDMETHODCALLTYPE SetILInstrumentedCodeMap(FunctionID, BOOL, ULONG, COR_IL_MAP[]) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetInprocInspectionInterface(IUnknown **) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetInprocInspectionIThisThread(IUnknown **) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetThreadContext(ThreadID, ContextID *) = 0;
    virtual HRESULT STDMETHODCALLTYPE BeginInprocDebugging(BOOL, DWORD *) = 0;
    virtual HRESULT STDMETHODCALLTYPE EndInprocDebugging(DWORD) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetILToNativeMapping(FunctionID, ULONG32, ULONG32 *, COR_DEBUG_IL_TO_NATIVE_MAP[]) = 0;
};

struct __declspec(uuid("176FBED1-A55C-4796-98CA-A9DA0EF883E7")) ICorProfilerCallback : IUnknown
{
    virtual HRESULT STDMETHODCALLTYPE Initialize(IUnknown *) = 0;
    virtual HRESULT STDMETHODCALLTYPE Shutdown() = 0;
    virtual HRESULT STDMETHODCALLTYPE AppDomainCreationStarted(AppDomainID) = 0;
    virtual HRESULT STDMETHODCALLTYPE AppDomainCreationFinished(AppDomainID, HRESULT) = 0;
    virtual HRESULT STDMETHODCALLTYPE AppDomainShutdownStarted(AppDomainID) = 0;
    virtual HRESULT STDMETHODCALLTYPE AppDomainShutdownFinished(AppDomainID, HRESULT) = 0;
    virtual HRESULT STDMETHODCALLTYPE AssemblyLoadStarted(AssemblyID) = 0;
    virtual HRESULT STDMETHODCALLTYPE AssemblyLoadFinished(AssemblyID, HRESULT) = 0;
    virtual HRESULT STDMETHODCALLTYPE AssemblyUnloadStarted(AssemblyID) = 0;
    virtual HRESULT STDMETHODCALLTYPE AssemblyUnloadFinished(AssemblyID, HRESULT) = 0;
    virtual HRESULT STDMETHODCALLTYPE ModuleLoadStarted(ModuleID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ModuleLoadFinished(ModuleID, HRESULT) = 0;
    virtual HRESULT STDMETHODCALLTYPE ModuleUnloadStarted(ModuleID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ModuleUnloadFinished(ModuleID, HRESULT) = 0;
    virtual HRESULT STDMETHODCALLTYPE ModuleAttachedToAssembly(ModuleID, AssemblyID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ClassLoadStarted(ClassID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ClassLoadFinished(ClassID, HRESULT) = 0;
    virtual HRESULT STDMETHODCALLTYPE ClassUnloadStarted(ClassID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ClassUnloadFinished(ClassID, HRESULT) = 0;
    virtual HRESULT STDMETHODCALLTYPE FunctionUnloadStarted(FunctionID) = 0;
    virtual HRESULT STDMETHODCALLTYPE JITCompilationStarted(FunctionID, BOOL) = 0;
    virtual HRESULT STDMETHODCALLTYPE JITCompilationFinished(FunctionID, HRESULT, BOOL) = 0;
    virtual HRESULT STDMETHODCALLTYPE JITCachedFunctionSearchStarted(FunctionID, BOOL *) = 0;
    virtual HRESULT STDMETHODCALLTYPE JITCachedFunctionSearchFinished(FunctionID, COR_PRF_JIT_CACHE) = 0;
    virtual HRESULT STDMETHODCALLTYPE JITFunctionPitched(FunctionID) = 0;
    virtual HRESULT STDMETHODCALLTYPE JITInlining(FunctionID, FunctionID, BOOL *) = 0;
    virtual HRESULT STDMETHODCALLTYPE ThreadCreated(ThreadID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ThreadDestroyed(ThreadID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ThreadAssignedToOSThread(ThreadID, DWORD) = 0;
    virtual HRESULT STDMETHODCALLTYPE RemotingClientInvocationStarted() = 0;
    virtual HRESULT STDMETHODCALLTYPE RemotingClientSendingMessage(GUID *, BOOL) = 0;
    virtual HRESULT STDMETHODCALLTYPE RemotingClientReceivingReply(GUID *, BOOL) = 0;
    virtual HRESULT STDMETHODCALLTYPE RemotingClientInvocationFinished() = 0;
    virtual HRESULT STDMETHODCALLTYPE RemotingServerReceivingMessage(GUID *, BOOL) = 0;
    virtual HRESULT STDMETHODCALLTYPE RemotingServerInvocationStarted() = 0;
    virtual HRESULT STDMETHODCALLTYPE RemotingServerInvocationReturned() = 0;
    virtual HRESULT STDMETHODCALLTYPE RemotingServerSendingReply(GUID *, BOOL) = 0;
    virtual HRESULT STDMETHODCALLTYPE UnmanagedToManagedTransition(FunctionID, COR_PRF_TRANSITION_REASON) = 0;
    virtual HRESULT STDMETHODCALLTYPE ManagedToUnmanagedTransition(FunctionID, COR_PRF_TRANSITION_REASON) = 0;
    virtual HRESULT STDMETHODCALLTYPE RuntimeSuspendStarted(COR_PRF_SUSPEND_REASON) = 0;
    virtual HRESULT STDMETHODCALLTYPE RuntimeSuspendFinished() = 0;
    virtual HRESULT STDMETHODCALLTYPE RuntimeSuspendAborted() = 0;
    virtual HRESULT STDMETHODCALLTYPE RuntimeResumeStarted() = 0;
    virtual HRESULT STDMETHODCALLTYPE RuntimeResumeFinished() = 0;
    virtual HRESULT STDMETHODCALLTYPE RuntimeThreadSuspended(ThreadID) = 0;
    virtual HRESULT STDMETHODCALLTYPE RuntimeThreadResumed(ThreadID) = 0;
    virtual HRESULT STDMETHODCALLTYPE MovedReferences(ULONG, ObjectID[], ObjectID[], ULONG[]) = 0;
    virtual HRESULT STDMETHODCALLTYPE ObjectAllocated(ObjectID, ClassID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ObjectsAllocatedByClass(ULONG, ClassID[], ULONG[]) = 0;
    virtual HRESULT STDMETHODCALLTYPE ObjectReferences(ObjectID, ClassID, ULONG, ObjectID[]) = 0;
    virtual HRESULT STDMETHODCALLTYPE RootReferences(ULONG, ObjectID[]) = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionThrown(ObjectID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionSearchFunctionEnter(FunctionID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionSearchFunctionLeave() = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionSearchFilterEnter(FunctionID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionSearchFilterLeave() = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionSearchCatcherFound(FunctionID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionOSHandlerEnter(UINT_PTR) = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionOSHandlerLeave(UINT_PTR) = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionUnwindFunctionEnter(FunctionID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionUnwindFunctionLeave() = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionUnwindFinallyEnter(FunctionID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionUnwindFinallyLeave() = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionCatcherEnter(FunctionID, ObjectID) = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionCatcherLeave() = 0;
    virtual HRESULT STDMETHODCALLTYPE COMClassicVTableCreated(ClassID, REFGUID, void *, ULONG) = 0;
    virtual HRESULT STDMETHODCALLTYPE COMClassicVTableDestroyed(ClassID, REFGUID, void *) = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionCLRCatcherFound() = 0;
    virtual HRESULT STDMETHODCALLTYPE ExceptionCLRCatcherExecute() = 0;
};

struct __declspec(uuid("8A8CC829-CCF2-49FE-BBAE-0F022228071A")) ICorProfilerCallback2 : ICorProfilerCallback
{
    virtual HRESULT STDMETHODCALLTYPE ThreadNameChanged(ThreadID, ULONG, WCHAR[]) = 0;
    virtual HRESULT STDMETHODCALLTYPE GarbageCollectionStarted(int, BOOL[], COR_PRF_GC_REASON) = 0;
    virtual HRESULT STDMETHODCALLTYPE SurvivingReferences(ULONG, ObjectID[], ULONG[]) = 0;
    virtual HRESULT STDMETHODCALLTYPE GarbageCollectionFinished() = 0;
    virtual HRESULT STDMETHODCALLTYPE FinalizeableObjectQueued(DWORD, ObjectID) = 0;
    virtual HRESULT STDMETHODCALLTYPE RootReferences2(ULONG, ObjectID[], COR_PRF_GC_ROOT_KIND[], COR_PRF_GC_ROOT_FLAGS[], UINT_PTR[]) = 0;
    virtual HRESULT STDMETHODCALLTYPE HandleCreated(GCHandleID, ObjectID) = 0;
    virtual HRESULT STDMETHODCALLTYPE HandleDestroyed(GCHandleID) = 0;
};

struct __declspec(uuid("BA3FEE4C-ECB9-4E41-83B7-183FA41CD859")) ICorProfilerMetadataEmit : IUnknown
{
    virtual HRESULT STDMETHODCALLTYPE SetModuleProps(LPCWSTR) = 0;
    virtual HRESULT STDMETHODCALLTYPE Save(LPCWSTR, DWORD) = 0;
    virtual HRESULT STDMETHODCALLTYPE SaveToStream(IStream *, DWORD) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetSaveSize(ULONG, DWORD *) = 0;
    virtual HRESULT STDMETHODCALLTYPE DefineTypeDef(LPCWSTR, DWORD, mdToken, mdToken[], mdTypeDef *) = 0;
    virtual HRESULT STDMETHODCALLTYPE DefineNestedType(LPCWSTR, DWORD, mdToken, mdToken[], mdTypeDef, mdTypeDef *) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetHandler(IUnknown *) = 0;
    virtual HRESULT STDMETHODCALLTYPE DefineMethod(mdTypeDef, LPCWSTR, DWORD, PCCOR_SIGNATURE, ULONG, ULONG, DWORD, mdMethodDef *) = 0;
    virtual HRESULT STDMETHODCALLTYPE DefineMethodImpl(mdTypeDef, mdToken, mdToken) = 0;
    virtual HRESULT STDMETHODCALLTYPE DefineTypeRefByName(mdToken, LPCWSTR, mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE DefineImportType(IUnknown *, const void *, ULONG, IMetaDataImport *, mdTypeDef, IUnknown *, mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE DefineMemberRef(mdToken, LPCWSTR, PCCOR_SIGNATURE, ULONG, mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE DefineImportMember(IUnknown *, const void *, ULONG, IMetaDataImport *, mdToken, IUnknown *, mdToken, mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE DefineEvent(mdTypeDef, LPCWSTR, DWORD, mdToken, mdMethodDef, mdMethodDef, mdMethodDef, mdMethodDef[], mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetClassLayout(mdTypeDef, DWORD, void *, ULONG) = 0;
    virtual HRESULT STDMETHODCALLTYPE DeleteClassLayout(mdTypeDef) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetFieldMarshal(mdToken, PCCOR_SIGNATURE, ULONG) = 0;
    virtual HRESULT STDMETHODCALLTYPE DeleteFieldMarshal(mdToken) = 0;
    virtual HRESULT STDMETHODCALLTYPE DefinePermissionSet(mdToken, DWORD, void const *, ULONG, mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetRVA(mdMethodDef, ULONG) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetTokenFromSig(PCCOR_SIGNATURE, ULONG, mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE DefineModuleRef(LPCWSTR, mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE SetParent(mdToken, mdToken) = 0;
    virtual HRESULT STDMETHODCALLTYPE GetTokenFromTypeSpec(PCCOR_SIGNATURE, ULONG, mdToken *) = 0;
    virtual HRESULT STDMETHODCALLTYPE SaveToMemory(void *, ULONG) = 0;
    virtual HRESULT STDMETHODCALLTYPE DefineUserString(LPCWSTR, ULONG, mdString *) = 0;
};

inline bool IsFailed(HRESULT hr) { return FAILED(hr); }
