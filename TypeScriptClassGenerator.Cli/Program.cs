using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using TypeDefinition = Mono.Cecil.TypeDefinition;

namespace TypeScriptClassGenerator.Cli
{
    public static class Program
    {
        private const string GenerateCustomAttribute = "TypeScriptClassGenerator.GenerateTypeScript";

        public static void Main()
        {
            var path = GetProjectDllDirectory();
            var dlls = GetDlls(path);
            var types = new List<TypeDefinition>();
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

        private static IEnumerable<TypeDefinition> GetTypesFromDll(string dllPath)
        {
            var dll = Assembly.LoadFile(dllPath);
            var typesToReturn = new List<Type>();
            try
            {
                return AssemblyDefinition.ReadAssembly(dllPath).MainModule.Types.Where(_ =>
                    _.CustomAttributes.Any(x => x.AttributeType.FullName == GenerateCustomAttribute));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return new List<TypeDefinition>();
            }
        }

        private static void CreateClassFiles(TypeDefinition type)
        {
            var importedClasses = new List<string>();

            var fields = type.Fields;

            var classContent = $"export class {type.Name} {{\n";

            foreach (var field in fields)
            {
                classContent += $"\t{MapField(field)}\n";
            }

            classContent += "}";

            using (var fileStream =
                File.Create($"ts-classes/{type.Name.ToKebabCase()}.model.ts"))
            {
                var content = new UTF8Encoding(true).GetBytes(classContent);
                fileStream.Write(content, 0, content.Length);
            }
        }

        private static string MapField(Mono.Cecil.FieldDefinition field)
        {
            var fieldName = field.Name.Split(">")[0].Split("<")[1];

            const string list = "System.Collections.Generic.List";

            string typeName = null;

            if (field.FieldType.FullName.IndexOf(list, StringComparison.Ordinal) >= 0)
            {
                var splitString = field.FieldType.FullName.Split("<")[1].Split(">")[0].Split(".");
                typeName = $"{splitString[^1]}[]";
            }
            else
            {
                var splitString = field.FieldType.FullName.Split(".");
                typeName = splitString[^1];
            }

            return $"{char.ToLower(fieldName[0]) + fieldName.Substring(1)}: {MapType(typeName)};";
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