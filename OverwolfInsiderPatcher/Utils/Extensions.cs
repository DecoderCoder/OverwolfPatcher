using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Bluscream;

public static class Extensions
{
    #region Reflection

    public static Dictionary<string, object> ToDictionary(this object instanceToConvert)
    {
        return instanceToConvert.GetType()
          .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
          .ToDictionary(
          propertyInfo => propertyInfo.Name,
          propertyInfo => Extensions.ConvertPropertyToDictionary(propertyInfo, instanceToConvert));

    }

    private static object ConvertPropertyToDictionary(PropertyInfo propertyInfo, object owner)
    {
        Type propertyType = propertyInfo.PropertyType;
        object propertyValue = propertyInfo.GetValue(owner);

        // If property is a collection don't traverse collection properties but the items instead
        if (!propertyType.Equals(typeof(string)) && (typeof(ICollection<>).Name.Equals(propertyValue.GetType().BaseType.Name) || typeof(Collection<>).Name.Equals(propertyValue.GetType().BaseType.Name)))
        {
            var collectionItems = new List<Dictionary<string, object>>();
            var count = (int)propertyType.GetProperty("Count").GetValue(propertyValue);
            PropertyInfo indexerProperty = propertyType.GetProperty("Item");

            // Convert collection items to dictionary
            for (var index = 0; index < count; index++)
            {
                object item = indexerProperty.GetValue(propertyValue, new object[] { index });
                PropertyInfo[] itemProperties = item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

                if (itemProperties.Any())
                {
                    Dictionary<string, object> dictionary = itemProperties
                      .ToDictionary(
                        subtypePropertyInfo => subtypePropertyInfo.Name,
                        subtypePropertyInfo => Extensions.ConvertPropertyToDictionary(subtypePropertyInfo, item));
                    collectionItems.Add(dictionary);
                }
            }

            return collectionItems;
        }

        // If property is a string stop traversal (ignore that string is a char[])
        if (propertyType.IsPrimitive || propertyType.Equals(typeof(string)))
        {
            return propertyValue;
        }

        PropertyInfo[] properties = propertyType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        if (properties.Any())
        {
            return properties.ToDictionary(
                                subtypePropertyInfo => subtypePropertyInfo.Name,
                                subtypePropertyInfo => (object)Extensions.ConvertPropertyToDictionary(subtypePropertyInfo, propertyValue));
        }

        return propertyValue;
    }
    #endregion
    #region DateTime
    public static bool ExpiredSince(this DateTime dateTime, int minutes)
    {
        return (dateTime - DateTime.Now).TotalMinutes < minutes;
    }
    public static TimeSpan StripMilliseconds(this TimeSpan time)
    {
        return new TimeSpan(time.Days, time.Hours, time.Minutes, time.Seconds);
    }
    #endregion
    #region DirectoryInfo
    public static DirectoryInfo Combine(this DirectoryInfo dir, params string[] paths)
    {
        var final = dir.FullName;
        foreach (var path in paths)
        {
            final = Path.Combine(final, path);
        }
        return new DirectoryInfo(final);
    }
    public static bool IsEmpty(this DirectoryInfo directory)
    {
        return !Directory.EnumerateFileSystemEntries(directory.FullName).Any();
    }
    public static string StatusString(this DirectoryInfo directory, bool existsInfo = false)
    {
        if (directory is null) return " (is null ❌)";
        if (File.Exists(directory.FullName)) return " (is file ❌)";
        if (!directory.Exists) return " (does not exist ❌)";
        if (directory.IsEmpty()) return " (is empty ⚠️)";
        return existsInfo ? " (exists ✅)" : string.Empty;
    }
    public static void Copy(this DirectoryInfo source, DirectoryInfo target, bool overwrite = false)
    {
        Directory.CreateDirectory(target.FullName);
        foreach (FileInfo fi in source.GetFiles())
            fi.CopyTo(Path.Combine(target.FullName, fi.Name), overwrite);
        foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            Copy(diSourceSubDir, target.CreateSubdirectory(diSourceSubDir.Name));
    }
    public static bool Backup(this DirectoryInfo directory, bool overwrite = false)
    {
        if (!directory.Exists) return false;
        var backupDirPath = directory.FullName + ".bak";
        if (Directory.Exists(backupDirPath) && !overwrite) return false;
        Directory.CreateDirectory(backupDirPath);
        foreach (FileInfo fi in directory.GetFiles()) fi.CopyTo(Path.Combine(backupDirPath, fi.Name), overwrite);
        foreach (DirectoryInfo diSourceSubDir in directory.GetDirectories())
        {
            diSourceSubDir.Copy(Directory.CreateDirectory(Path.Combine(backupDirPath, diSourceSubDir.Name)), overwrite);
        }
        return true;
    }
    #endregion
    #region FileInfo
    public static FileInfo CombineFile(this DirectoryInfo dir, params string[] paths)
    {
        var final = dir.FullName;
        foreach (var path in paths)
        {
            final = Path.Combine(final, path);
        }
        return new FileInfo(final);
    }
    //public static FileInfo CombineFile(this DirectoryInfo absoluteDir, FileInfo relativeFile) => new FileInfo(Path.Combine(absoluteDir.FullName, relativeFile.OriginalPath));
    public static FileInfo Combine(this FileInfo file, params string[] paths)
    {
        var final = file.DirectoryName;
        foreach (var path in paths)
        {
            final = Path.Combine(final, path);
        }
        return new FileInfo(final);
    }
    public static string FileNameWithoutExtension(this FileInfo file)
    {
        return Path.GetFileNameWithoutExtension(file.Name);
    }
    /*public static string Extension(this FileInfo file) {
        return Path.GetExtension(file.Name);
    }*/
    public static string StatusString(this FileInfo file, bool existsInfo = false)
    {
        if (file is null) return "(is null ❌)";
        if (Directory.Exists(file.FullName)) return "(is directory ❌)";
        if (!file.Exists) return "(does not exist ❌)";
        if (file.Length < 1) return "(is empty ⚠️)";
        return existsInfo ? "(exists ✅)" : string.Empty;
    }
    public static void AppendLine(this FileInfo file, string line)
    {
        try
        {
            if (!file.Exists) file.Create();
            File.AppendAllLines(file.FullName, new string[] { line });
        } catch { }
    }
    public static void WriteAllText(this FileInfo file, string text) => File.WriteAllText(file.FullName, text);
    public static string ReadAllText(this FileInfo file) => File.ReadAllText(file.FullName);
    public static List<string> ReadAllLines(this FileInfo file) => File.ReadAllLines(file.FullName).ToList();
    public static bool Backup(this FileInfo file, bool overwrite = false)
    {
        if (!file.Exists) return false;
        var backupFilePath = file.FullName + ".bak";
        if (File.Exists(backupFilePath) && !overwrite) return false;
        File.Copy(file.FullName, backupFilePath, overwrite);
        return true;
    }
    public static bool Restore(this FileInfo file, bool overwrite = false)
    {
        if (!file.Exists || !File.Exists(file.FullName + ".bak")) return false;
        if (overwrite) File.Delete(file.FullName);
        File.Move(file.FullName + ".bak", file.FullName);
        return true;
    }
    #endregion
    #region UI
    #endregion
    #region Object
    #endregion
    #region String
    public static IEnumerable<string> SplitToLines(this string input)
    {
        if (input == null)
        {
            yield break;
        }

        using (System.IO.StringReader reader = new System.IO.StringReader(input))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }
    }
    public static string ToTitleCase(this string source, string langCode = "en-US")
    {
        return new CultureInfo(langCode, false).TextInfo.ToTitleCase(source);
    }
    public static bool Contains(this string source, string toCheck, StringComparison comp)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }
    public static bool IsNullOrEmpty(this string source)
    {
        return string.IsNullOrEmpty(source);
    }
    public static string[] Split(this string source, string split, int count = -1, StringSplitOptions options = StringSplitOptions.None)
    {
        if (count != -1) return source.Split(new string[] { split }, count, options);
        return source.Split(new string[] { split }, options);
    }
    public static string Remove(this string Source, string Replace)
    {
        return Source.Replace(Replace, string.Empty);
    }
    public static string ReplaceLastOccurrence(this string Source, string Find, string Replace)
    {
        int place = Source.LastIndexOf(Find);
        if (place == -1)
            return Source;
        string result = Source.Remove(place, Find.Length).Insert(place, Replace);
        return result;
    }
    public static string EscapeLineBreaks(this string source)
    {
        return Regex.Replace(source, @"\r\n?|\n", @"\$&");
    }
    public static string Ext(this string text, string extension)
    {
        return text + "." + extension;
    }
    public static string Quote(this string text)
    {
        return SurroundWith(text, "\"");
    }
    public static string Enclose(this string text)
    {
        return SurroundWith(text, "(", ")");
    }
    public static string Brackets(this string text)
    {
        return SurroundWith(text, "[", "]");
    }
    public static string SurroundWith(this string text, string surrounds)
    {
        return surrounds + text + surrounds;
    }
    public static string SurroundWith(this string text, string starts, string ends)
    {
        return starts + text + ends;
    }
    #endregion
    #region Dict
    public static void AddSafe(this IDictionary<string, string> dictionary, string key, string value)
    {
        if (!dictionary.ContainsKey(key))
            dictionary.Add(key, value);
    }
    public static Dictionary<string, object?> MergeWith(this Dictionary<string, object?> sourceDict, Dictionary<string, object?> destDict)
    {
        foreach (var kvp in sourceDict)
        {
            if (destDict.ContainsKey(kvp.Key))
            {
                Console.WriteLine($"Key '{kvp.Key}' already exists and will be overwritten.");
            }
            destDict[kvp.Key] = kvp.Value;
        }
        return destDict;
    }
    public static Dictionary<string, object> MergeRecursiveWith(this Dictionary<string, object> sourceDict, Dictionary<string, object> targetDict)
    {
        foreach (var kvp in sourceDict)
        {
            if (targetDict.TryGetValue(kvp.Key, out var existingValue))
            {
                if (existingValue is Dictionary<string, object> existingDict && kvp.Value is Dictionary<string, object> sourceDictValue)
                {
                    sourceDictValue.MergeRecursiveWith(existingDict);
                } else if (kvp.Value is null)
                {
                    targetDict.Remove(kvp.Key);
                    Console.WriteLine($"Removed key '{kvp.Key}' as it was set to null in the source dictionary.");
                } else
                {
                    targetDict[kvp.Key] = kvp.Value;
                    Console.WriteLine($"Overwriting existing value for key '{kvp.Key}'.");
                }
            } else
            {
                targetDict[kvp.Key] = kvp.Value;
            }
        }

        return targetDict;
    }

    #endregion
    #region List
    public static string ToQueryString(this NameValueCollection nvc)
    {
        if (nvc == null) return string.Empty;

        StringBuilder sb = new StringBuilder();

        foreach (string key in nvc.Keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;

            string[] values = nvc.GetValues(key);
            if (values == null) continue;

            foreach (string value in values)
            {
                sb.Append(sb.Length == 0 ? "?" : "&");
                sb.AppendFormat("{0}={1}", key, value);
            }
        }

        return sb.ToString();
    }
    public static bool GetBool(this NameValueCollection collection, string key, bool defaultValue = false)
    {
        if (!collection.AllKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) return false;
        var trueValues = new string[] { true.ToString(), "yes", "1" };
        if (trueValues.Contains(collection[key], StringComparer.OrdinalIgnoreCase)) return true;
        var falseValues = new string[] { false.ToString(), "no", "0" };
        if (falseValues.Contains(collection[key], StringComparer.OrdinalIgnoreCase)) return true;
        return defaultValue;
    }
    public static string GetString(this NameValueCollection collection, string key)
    {
        if (!collection.AllKeys.Contains(key)) return collection[key];
        return null;
    }
    public static T PopFirst<T>(this IEnumerable<T> list) => list.ToList().PopAt(0);
    public static T PopLast<T>(this IEnumerable<T> list) => list.ToList().PopAt(list.Count() - 1);
    public static T PopAt<T>(this List<T> list, int index)
    {
        T r = list.ElementAt<T>(index);
        list.RemoveAt(index);
        return r;
    }
    #endregion
    #region Uri
    private static readonly Regex QueryRegex = new Regex(@"[?&](\w[\w.]*)=([^?&]+)");
    public static IReadOnlyDictionary<string, string> ParseQueryString(this Uri uri)
    {
        var match = QueryRegex.Match(uri.PathAndQuery);
        var paramaters = new Dictionary<string, string>();
        while (match.Success)
        {
            paramaters.Add(match.Groups[1].Value, match.Groups[2].Value);
            match = match.NextMatch();
        }
        return paramaters;
    }
    #endregion
    #region Enum
    public static DescriptionAttribute GetEnumDescriptionAttribute<T>(
    this T value) where T : struct
    {
        // The type of the enum, it will be reused.
        Type type = typeof(T);

        // If T is not an enum, get out.
        if (!type.IsEnum)
            throw new InvalidOperationException(
                "The type parameter T must be an enum type.");

        // If the value isn't defined throw an exception.
        if (!Enum.IsDefined(type, value))
            throw new InvalidEnumArgumentException(
                "value", Convert.ToInt32(value), type);

        // Get the static field for the value.
        FieldInfo fi = type.GetField(value.ToString(),
            BindingFlags.Static | BindingFlags.Public);

        // Get the description attribute, if there is one.
        return fi.GetCustomAttributes(typeof(DescriptionAttribute), true).
            Cast<DescriptionAttribute>().SingleOrDefault();
    }
    public static string? GetName(this Type enumType, object value) => Enum.GetName(enumType, value);
    #endregion
    #region Task
    public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
    {
        using (var timeoutCancellationTokenSource = new CancellationTokenSource())
        {
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
            if (completedTask == task)
            {
                timeoutCancellationTokenSource.Cancel();
                return await task;  // Very important in order to propagate exceptions
            } else
            {
                return default(TResult);
            }
        }
    }
    #endregion
    #region bool
    public static string ToYesNo(this bool input) => input ? "Yes" : "No";
    public static string ToEnabledDisabled(this bool input) => input ? "Enabled" : "Disabled";
    public static string ToOnOff(this bool input) => input ? "On" : "Off";
    #endregion
}