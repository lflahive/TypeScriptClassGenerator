using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TypeScriptClassGenerator.Cli
{
    public static class Program
    {
        private const string GenerateCustomAttribute = "TypeScriptClassGenerator.GenerateTypeScript";

        public static void Main()
        {
            var path = GetProjectDllDirectory();
            var dlls = GetDlls(path);
            var types = new List<Type>();
            dlls.ForEach(dll => types.AddRange(GetTypesFromDll(dll)));
            // Create the ts-classes folder if it doesn't exist
            Directory.CreateDirectory("ts-classes");
            types.ForEach(CreateClassFiles);
        }

        private static string GetProjectDllDirectory()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "bin\\Debug");
            var directoryInfo = new DirectoryInfo(path);
            var directory = directoryInfo.GetDirectories("netcoreapp*").FirstOrDefault();

            if (directory == null || !directory.GetFiles("*.dll").Any())
                throw new Exception("Command must be ran from a folder containing a .csproj file.");

            return directory.FullName;
        }

        private static List<string> GetDlls(string directory)
        {
            var dlls = new List<string>();
            dlls.AddRange(Directory.GetFiles(directory, "*.dll"));

            return dlls;
        }

        private static IEnumerable<Type> GetTypesFromDll(string dllPath)
        {
            var dll = Assembly.LoadFile(dllPath);

            return dll.GetExportedTypes()
                .Where(type => type.CustomAttributes.Any(x => x.AttributeType.FullName == GenerateCustomAttribute));
        }

        private static void CreateClassFiles(Type type)
        {
            var importedClasses = new List<string>();

            var fields = GetFields(type);

            var classContent = $"export class {type.Name} {{\n";

            foreach (var field in fields)
            {
                var mappedField = MapField(field);
                classContent += $"\t{mappedField.Field}\n";
                if (mappedField.IsClass)
                    importedClasses.Add(field.Name.Split(">")[0].Split("<")[1]);
            }

            classContent += "}";

            using (var fileStream =
                File.Create($"ts-classes/{type.Name.ToKebabCase()}.model.ts"))
            {
                Console.WriteLine(classContent);
                var content = new UTF8Encoding(true).GetBytes(classContent);
                fileStream.Write(content, 0, content.Length);
            }
        }

        private static List<FieldInfo> GetFields(Type type)
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                  BindingFlags.NonPublic).ToList();
        }

        private static (string Field, bool IsClass) MapField(FieldInfo field)
        {
            var isClass = false;
            var fieldName = field.Name.Split(">")[0].Split("<")[1];

            var list = "System.Collections.Generic.List";

            string typeName = null;

            if (field.FieldType.IsClass)
            {
                isClass = true;
                var fullTypeName = field.FieldType.AssemblyQualifiedName.Split(",")[0].Split(".");
                typeName = fullTypeName[^1];
            }
            else
            {
                typeName = field.FieldType.AssemblyQualifiedName.IndexOf(list) >= 0
                    ? $"{field.FieldType.AssemblyQualifiedName.Split("[[")[1].Split(",")[0].Split(".")[1]}[]"
                    : field.FieldType.AssemblyQualifiedName.Split(",")[0].Split(".")[1];
            }

            return ($"{char.ToLower(fieldName[0]) + fieldName.Substring(1)}: {MapType(typeName)};", isClass);
        }

        private static string MapType(string typeName)
        {
            switch (typeName)
            {
                case "Guid":
                case "String":
                    return "string";
                case "String[]":
                    return "string[]";
                case "Int32":
                case "Int64":
                case "Single":
                    return "number";
                case "Int32[]":
                case "Int64[]":
                case "Single[]":
                    return "number[]";
                case "Boolean":
                    return "boolean";
                case "DateTime":
                case "DateTimeOffset":
                    return "Date";
                default:
                    return typeName;
            }
        }
    }
}