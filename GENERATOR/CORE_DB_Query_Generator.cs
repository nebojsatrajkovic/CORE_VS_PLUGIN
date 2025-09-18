using CORE_VS_PLUGIN.GENERATOR.Model;
using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CORE_VS_PLUGIN.GENERATOR
{
    public static class CORE_DB_Query_Generator
    {
        public static void GenerateQuery(Project project, CORE_DB_QUERY_XML_Template xmlTemplate, Dictionary<string, string> parameters)
        {
            var pathTokens = parameters[CORE_DB_QUERY_Metadata.File_Path].Split('\\').ToList();
            pathTokens = pathTokens.Take(pathTokens.Count - 2).ToList();

            var generatedClassNamespace = parameters[CORE_DB_QUERY_Metadata.Namespace];
            var generatedClassName = parameters[CORE_DB_QUERY_Metadata.ClassName];

            var containingFolder = string.Join("\\", pathTokens);

            #region sql

            var sqlDirectory = $"{containingFolder}\\SQL";

            var sqlFilePath = $"{sqlDirectory}\\{generatedClassName}.sql";

            Directory.CreateDirectory(sqlDirectory);

            File.WriteAllText(sqlFilePath, xmlTemplate.SQL);

            CopyToProject(project, sqlFilePath).SetBuildAction(CORE_DB_ProjectItem_BuildAction.EmbeddedResource);

            #endregion sql

            string classTemplate;
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("CORE_VS_PLUGIN.GENERATOR.Templates.Query.DB_QUERY_TEMPLATE.txt")))
            {
                classTemplate = reader.ReadToEnd();
            }

            var projectNameToken = pathTokens.First(x => x.Equals(project.Name));
            var index = pathTokens.IndexOf(projectNameToken);
            var commandLocationTokens = pathTokens.Skip(index).Take(pathTokens.Count - index).ToList();
            commandLocationTokens.Add("SQL");
            commandLocationTokens.Add($"{generatedClassName}.sql");
            var commandLocation = string.Join(".", commandLocationTokens);

            // base data
            {
                classTemplate = classTemplate.Replace("${NAMESPACE}", generatedClassNamespace);
                classTemplate = classTemplate.Replace("${CLASS_NAME}", generatedClassName);

                if (xmlTemplate.Parameter != null && !string.IsNullOrEmpty(xmlTemplate.Parameter.ClassName))
                {
                    classTemplate = classTemplate.Replace("${PARAMETER_TYPE}", xmlTemplate.Parameter.ClassName);
                }
                else
                {
                    classTemplate = classTemplate.Replace(", ${PARAMETER_TYPE} parameter", string.Empty);
                }

                classTemplate = classTemplate.Replace("${COMMAND_LOCATION}", commandLocation);
            }

            // classes
            {
                (string rawClassTemplate, List<string> customClasses) = GenerateClasses(xmlTemplate);

                classTemplate = classTemplate.Replace("${RAW_CLASS}", rawClassTemplate ?? string.Empty);

                if (customClasses?.Any() == true)
                {
                    var combinedString = string.Join($"{Environment.NewLine}{Environment.NewLine}", customClasses);

                    classTemplate = classTemplate.Replace("${CUSTOM_CLASSES}", combinedString);
                }
                else
                {
                    classTemplate = classTemplate.Replace("${CUSTOM_CLASSES}", string.Empty);
                }

                // if we have grouping then we will evaluate the result class definition and transform results accordingly
                if (!string.IsNullOrEmpty(xmlTemplate?.Result?.ResultClass?.GroupBy))
                {
                    classTemplate = classTemplate.Replace("${RETURN_PLACEHOLDER}", "var result = ${RESULT_TYPE}_Raw.Convert(results).${RESULT_GROUPING_TYPE};");

                    classTemplate = classTemplate.Replace("${RESULT_TYPE}", xmlTemplate.Result.ResultClass.Name);
                    classTemplate = classTemplate.Replace("${RESULT_GROUPING_TYPE}", xmlTemplate.Result.ResultClass.IsCollection ? "ToList()" : "FirstOrDefault()");
                    classTemplate = classTemplate.Replace("${RETURN_TYPE}", xmlTemplate.Result.ResultClass.IsCollection ? $"List<{xmlTemplate.Result.ResultClass.Name}>" : xmlTemplate.Result.ResultClass.Name);
                }
                // if we don't use any data grouping then we should return raw data as it is retrieved from the database
                else
                {
                    classTemplate = classTemplate.Replace("${RESULT_TYPE}", $"{xmlTemplate.Result.ResultClass.Name}");
                    classTemplate = classTemplate.Replace("${RETURN_TYPE}", $"List<{xmlTemplate.Result.ResultClass.Name}_Raw>");

                    classTemplate = classTemplate.Replace("${RETURN_PLACEHOLDER}", "var result = results;");
                }
            }

            // data reader
            {
                string readerItemTemplate;
                using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("CORE_VS_PLUGIN.GENERATOR.Templates.Query.DB_QUERY_READER_ITEM_TEMPLATE.txt")))
                {
                    readerItemTemplate = reader.ReadToEnd();
                }

                var rawProperties = xmlTemplate.Result.ResultClass.ClassMember.SelectMany(x => x.GetAllClassMembers()).Where(x => !x.IsClass && !x.IsCollection).ToList();

                var sb = new StringBuilder();

                foreach (var property in rawProperties)
                {
                    var readerItem = readerItemTemplate.Replace("${PROPERTY_NAME}", property.Name);
                    readerItem = readerItem.Replace("${READER_TYPE}", GetReaderType(property.Type));

                    sb.Append(readerItem);
                    sb.AppendLine();
                }

                classTemplate = classTemplate.Replace("${READER_ITEMS}", sb.ToString());
            }

            // parameters
            {
                if (xmlTemplate.Parameter?.ClassMember?.Any() == true)
                {
                    string parameterTemplate;
                    using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("CORE_VS_PLUGIN.GENERATOR.Templates.Query.DB_QUERY_PARAMETER_TEMPLATE.txt")))
                    {
                        parameterTemplate = reader.ReadToEnd();
                    }

                    var sb = new StringBuilder();

                    foreach (var parameter in xmlTemplate.Parameter.ClassMember)
                    {
                        var parameterItem = parameterTemplate.Replace("${PARAMETER_REFERENCE_NAME}", $"_{parameter.Name}");
                        parameterItem = parameterItem.Replace("${PARAMETER_NAME}", $"@{parameter.Name}");
                        parameterItem = parameterItem.Replace("${PARAMETER_VALUE}", $"parameter.{parameter.Name}");

                        sb.Append(parameterItem);
                    }

                    classTemplate = classTemplate.Replace("${PARAMETERS_PLACEHOLDER}", sb.ToString());
                }
                else
                {
                    classTemplate = classTemplate.Replace("${PARAMETERS_PLACEHOLDER}", string.Empty);
                }
            }

            var classFilePath = $"{containingFolder}\\{generatedClassName}.cs";

            File.WriteAllText(classFilePath, classTemplate.FormatCode().FormatCode());

            CopyToProject(project, classFilePath);
        }

        public static ProjectItem CopyToProject(Project project, string fileLocation)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectItem = project.ProjectItems.AddFromFile(fileLocation);

            return projectItem;
        }

        public static ProjectItem SetBuildAction(this ProjectItem item, CORE_DB_ProjectItem_BuildAction buildAction)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var action = string.Empty;

            switch (buildAction)
            {
                case CORE_DB_ProjectItem_BuildAction.EmbeddedResource:
                    action = "EmbeddedResource";
                    break;
            }

            item.Properties.Item("ItemType").Value = action;

            return item;
        }

        public enum CORE_DB_ProjectItem_BuildAction
        {
            EmbeddedResource
        }

        public static (string rawClassTemplate, List<string> customClasses) GenerateClasses(CORE_DB_QUERY_XML_Template xmlTemplate)
        {
            var customClasses = new List<string>();

            var rawClassProperties = xmlTemplate.Result.ResultClass.ClassMember.SelectMany(x => x.GetAllClassMembers()).Where(x => !x.IsClass && !x.IsCollection).ToList();

            var hasGrouping = !string.IsNullOrEmpty(xmlTemplate.Result.ResultClass.GroupBy);

            var rawClassTemplate = GenerateClass($"{xmlTemplate.Result.ResultClass.Name}_Raw", rawClassProperties, hasGrouping).First();

            rawClassTemplate = rawClassTemplate.Replace("${CONVERT_METHOD}", RawDataConverterGenerator(xmlTemplate));

            if (xmlTemplate.Parameter?.ClassMember?.Any() == true)
            {
                customClasses.AddRange(GenerateClass(xmlTemplate.Parameter.ClassName, xmlTemplate.Parameter.ClassMember));
            }

            if (xmlTemplate.Result?.ResultClass?.ClassMember?.Any() == true)
            {
                customClasses.AddRange(GenerateClass(xmlTemplate.Result.ResultClass.Name, xmlTemplate.Result.ResultClass.ClassMember));
            }

            return (rawClassTemplate, customClasses);
        }

        public static string RawDataConverterGenerator(CORE_DB_QUERY_XML_Template xmlTemplate)
        {
            var result = string.Empty;

            var resultClass = xmlTemplate.Result.ResultClass;

            if (string.IsNullOrEmpty(resultClass.GroupBy))
            {
                return result;
            }

            string rootTemplate;
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("CORE_VS_PLUGIN.GENERATOR.Templates.Query.RawConverter.DB_QUERY_RAW_CONVERTER_ROOT.txt")))
            {
                rootTemplate = reader.ReadToEnd();
            }

            string propertyTemplate;
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("CORE_VS_PLUGIN.GENERATOR.Templates.Query.RawConverter.DB_QUERY_RAW_CONVERTER_PROPERTY.txt")))
            {
                propertyTemplate = reader.ReadToEnd();
            }

            string childTemplate;
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("CORE_VS_PLUGIN.GENERATOR.Templates.Query.RawConverter.DB_QUERY_RAW_CONVERTER_CHILD.txt")))
            {
                childTemplate = reader.ReadToEnd();
            }

            result = rootTemplate.Replace("${CLASS_NAME}", resultClass.Name);
            result = result.Replace("${GROUPING_KEY}", resultClass.GroupBy);
            result = result.Replace("${ELEMENT_NAME}", $"el_{resultClass.Name}");

            var sb = new StringBuilder();
            foreach (var item in resultClass.ClassMember.Where(x => !x.Name.Equals(resultClass.GroupBy)).ToList())
            {
                if (!item.IsClass && !item.IsCollection)
                {
                    var property = propertyTemplate.Replace("${PROPERTY_NAME}", item.Name);
                    property = property.Replace("${CLASS_NAME}", resultClass.Name);

                    sb.Append(property);
                    sb.AppendLine();
                }
            }

            result = result.Replace("${PROPERTIES}", sb.ToString());

            if (resultClass.ClassMember.Any(x => x.IsClass))
            {
                var gfunct_Name = $"gfunct_{resultClass.Name}";

                var members = resultClass.ClassMember.Where(x => x.IsClass && !string.IsNullOrEmpty(x.GroupBy)).ToList();

                var a = GetChildrenGroupingConverter(gfunct_Name, members, childTemplate, propertyTemplate);

                result = result.Replace("${CHILD_GROUPING}", a);
            }
            else
            {
                result = result.Replace("${CHILD_GROUPING}", string.Empty);
            }

            return result;
        }

        public static string GetChildrenGroupingConverter(string gfunct_Name, List<CORE_DB_QUERY_XML_ClassMember> classMembers, string childTemplate, string propertyTemplate)
        {
            var resultSB = new StringBuilder();

            foreach (var item in classMembers)
            {
                var childResult = childTemplate.Replace("${MEMBER_NAME}", item.Name);
                childResult = childResult.Replace("${GROUPING_KEY}", item.GroupBy);
                childResult = childResult.Replace("${CLASS_NAME}", item.Type);
                childResult = childResult.Replace("${ELEMENT_NAME}", $"el_{item.Name}");
                childResult = childResult.Replace("{GFUNCT_NAME}", $"{gfunct_Name}");

                if (item.ClassMembers?.Any() == true && item.ClassMembers.Any(x => !x.IsClass && !x.IsCollection && !x.Name.Equals(item.GroupBy)))
                {
                    var sb = new StringBuilder();

                    foreach (var member in item.ClassMembers.Where(x => !x.IsClass && !x.IsCollection && !x.Name.Equals(item.GroupBy)).ToList())
                    {
                        var property = propertyTemplate.Replace("${PROPERTY_NAME}", member.Name);
                        property = property.Replace("${CLASS_NAME}", $"{item.Name}");

                        sb.Append(property);
                        sb.AppendLine();
                    }

                    childResult = childResult.Replace("${PROPERTIES}", sb.ToString());
                }
                else
                {
                    childResult = childResult.Replace("${PROPERTIES}", string.Empty);
                }

                if (item.ClassMembers?.Any() == true && item.ClassMembers.Any(x => x.IsClass))
                {
                    var child_gfunctName = $"gfunct_{item.Name}";

                    var children = item.ClassMembers.Where(x => x.IsClass).ToList();

                    var c = GetChildrenGroupingConverter(child_gfunctName, children, childTemplate, propertyTemplate);

                    childResult = childResult.Replace("${CHILD_GROUPING}", c);
                }
                else
                {
                    childResult = childResult.Replace("${CHILD_GROUPING}", string.Empty);
                }

                if (item.IsCollection)
                {
                    childResult = childResult.Replace("${GROUPING_TYPE}", "ToList()");
                }
                else
                {
                    childResult = childResult.Replace("${GROUPING_TYPE}", "FirstOrDefault()");
                }

                resultSB.Append(childResult);
                resultSB.Append(",");
            }

            return resultSB.ToString();
        }

        public static List<string> GenerateClass(string className, List<CORE_DB_QUERY_XML_ClassMember> members, bool isRaw = false)
        {
            var customClasses = members.SelectMany(x => x.GetAllClassMembers()).Where(x => x.IsClass).ToList();

            var classes = new List<string>
            {
                GetClassString(className, members, isRaw)
            };

            customClasses.ForEach(x => classes.Add(GetClassString(x.Type, x.ClassMembers, isRaw)));

            return classes;
        }

        private static string GetClassString(string className, List<CORE_DB_QUERY_XML_ClassMember> members, bool isRaw = false)
        {
            var templateFile = isRaw ? "CORE_VS_PLUGIN.GENERATOR.Templates.Query.DB_QUERY_RAW_CLASS_TEMPLATE.txt" : "CORE_VS_PLUGIN.GENERATOR.Templates.Query.DB_QUERY_CLASS_TEMPLATE.txt";

            string classTemplate;
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(templateFile)))
            {
                classTemplate = reader.ReadToEnd();
            }

            classTemplate = classTemplate.Replace("${CLASS_NAME}", className);

            var sb = new StringBuilder();

            foreach (var member in members)
            {
                if (member.IsCollection)
                {
                    sb.Append($"public List<{member.Type}> {member.Name} {{ get; set; }}");
                    sb.AppendLine();
                }
                else
                {
                    sb.Append($"public {member.Type} {member.Name} {{ get; set; }}");
                    sb.AppendLine();
                }
            }

            classTemplate = classTemplate.Replace("${PROPERTIES}", sb.ToString());

            return classTemplate;
        }

        public static string FormatCode(this string csCode)
        {
            var tree = CSharpSyntaxTree.ParseText(csCode);
            var root = tree.GetRoot().NormalizeWhitespace();
            var ret = root.ToFullString();
            return ret;
        }

        public static string GetReaderType(string type)
        {
            string returnValue;

            switch (type.ToLower())
            {
                case "datetime": returnValue = "DateTime"; break;

                case "char":
                case "string": returnValue = "String"; break;

                case "guid": returnValue = "Guid"; break;

                case "float": returnValue = "Float"; break;
                case "double": returnValue = "Double"; break;

                case "ushort":
                case "short": returnValue = "Int16"; break;

                case "sbyte":
                case "byte": returnValue = "Byte"; break;

                case "ulong":
                case "long": returnValue = "Int64"; break;

                case "decimal": returnValue = "Decimal"; break;

                case "uint":
                case "int":
                case "integer": returnValue = "Int32"; break;

                case "boolean":
                case "bool": returnValue = "Boolean"; break;

                default: returnValue = type; break;
            }

            return returnValue;
        }
    }
}