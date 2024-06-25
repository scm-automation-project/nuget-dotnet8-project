using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace JsonAotTool
    {
    class Program
        {
        public static int OPERATION = 0; // 0 is bad, 1 is SETUP, 2 is RUN
        public static int WRITEMODE = 0; // 0 is append, 1 is overwrite
        public static bool WRITETOPROGRAM = false; // write to Program.cs or not
        public static readonly Dictionary<string, Action> RunParams = new Dictionary<string, Action>()
            {
                {"-a", SETWRITEMODE_APPEND },
                {"-o", SETWRITEMODE_OVERWRITE },
                {"-s", SETWRITETOPROGRAM },
            };
        public static readonly Dictionary<string, int> StartParam = new Dictionary<string, int>()
            {
                {"-run", 2},
                {"-setup", 1}
            };
        
        static void Main(string[] args)
            {

            if (args.Length == 0)
                {
                ShowHelp();
                return;
                }

            var command = args[0];

            for(int i = 0; i < args.Length; i++)
                {
                if(i == 0)
                    {
                    bool goodval = StartParam.TryGetValue(args[0], out int OPERATIONCODE);
                    if(goodval == true)
                        {
                        OPERATION = OPERATIONCODE;
                        }
                    } else if (i != 0)
                    {
                    if (RunParams.ContainsKey(args[i]))
                        {
                        try
                            {
                            RunParams[args[i]].Invoke();
                            }
                        catch
                            {
                            // nothing here
                            }
                        } else
                        {
                        Console.WriteLine($"Invalid parameter {args[i]} detected. Terminating.");
                        ShowHelp();
                        Environment.Exit(0);
                        return;
                        }
                    }
                }

            switch (OPERATION)
                {
                case 1:
                    CommandHandlers.HandleSetup();
                    break;
                case 2:
                    CommandHandlers.HandleRun();
                    break;
                default:
                    Console.WriteLine($"Invalid operation parameter {command} detected. Terminating.");
                    ShowHelp();
                    Environment.Exit(0);
                    break;
                }

            }

        #region RUNPARSE
        public static void SETWRITEMODE_APPEND()
            {
            if (OPERATION == 2)
                {
                WRITEMODE = 0;
                } else
                {
                Console.WriteLine("Parameter for --run detected. Terminating.");
                }
            }
        public static void SETWRITEMODE_OVERWRITE()
            {
            if (OPERATION == 2)
                {
                WRITEMODE = 1;
                } else
                {
                Console.WriteLine("Parameter for --run detected. Terminating.");
                Environment.Exit(0);
                return;
                }
            }
        public static void BADRUNPARAM()
            {
            Console.WriteLine("Invalid parameter for --run detected.");
            Environment.Exit(0);
            return;
            }
        #endregion

        #region RUNSETUP
        public static void SETWRITETOPROGRAM()
            {
            if (OPERATION == 1)
                {
                WRITETOPROGRAM = true;
                } else
                {
                Console.WriteLine("Parameter for --setup detected. Terminating.");
                Environment.Exit(0);
                return;
                }
            }

        #endregion

        private static void ShowHelp()
            {
            Console.Write(@"
JsonAot -- Quickly setup your project classes to use AOT compliant JSON serialization.
Add the attribute [JsonAot] to all classes slated for serialization or deserialization.
Run JsonAot in the root of your project solution and all *.cs class files will be scanned
and all classes slated for serialization willl automatically have their source gen code
generated. Use the built-in Serialize and Deserialize functions for seamless useage.

Parameter:          Desc:
   -setup           Setups initial JSONHandler file, making features accessible.
       -s           Places '");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("global using ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("JSONHandler;");
            Console.ResetColor();

            Console.WriteLine(@"' into local Program.cs
   -run             Scans project directory for all [JsonAot] classes, then 
                    automatically generates source gen. code for tagged classes.
       -a           During run, append all findings to JSONHandler (can result in 
                    duplicated classes/entries).
       -o           During run, completely overwrites JSONHandler (default).");
            Console.ReadKey();
            }

        class CommandHandlers
            {
            public static void HandleSetup()
                {

                JSONHandlerGenerator.CreateJSONHandlerFile();
                if (WRITETOPROGRAM == true)
                    {
                    JSONHandlerGenerator.ModifyProgramCS();
                    }
                }

            public static void HandleRun()
                {
                List<string> ClassNames = new List<string>();

                ScanSolutionForAttribute();
                UpdateJSONHandler(ClassNames);

                 void ScanSolutionForAttribute()
                    {
                    string solutionPath = Directory.GetCurrentDirectory();
                    var syntaxTrees = Directory.GetFiles(solutionPath, "*.cs", SearchOption.AllDirectories)
                        .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path)))
                        .ToList();

                    var compilation = CSharpCompilation.Create("MyCompilation", syntaxTrees)
                        .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                    var rootNamespace = syntaxTrees.Select(tree => compilation.GetSemanticModel(tree).Compilation.GlobalNamespace);

                    foreach (var ns in rootNamespace)
                        {
                        ProcessNamespace(ns);
#if DEBUG
                        Console.WriteLine($"Checking namespace {ns.Name}");
#endif
                        }
                    }

                 void ProcessNamespace(INamespaceSymbol ns, int x = 1)
                    {
                    string tabs_ = new string('\t', x);
                    foreach (var type in ns.GetTypeMembers())
                        {
                        ProcessType(type);
#if DEBBUG
                        Console.WriteLine($"{tabs_}Checking type {ns.Name}");
#endif
                        }

                    foreach (var nestedNs in ns.GetNamespaceMembers())
                        {
                        ProcessNamespace(nestedNs, x + 1);
#if DEBUG
                        Console.WriteLine($"{tabs_}Checking process {nestedNs.Name}");
#endif
                        }
                    }

                 void ProcessType(INamedTypeSymbol type)
                    {
                    foreach (var attribute in type.GetAttributes())
                        {
                        if (attribute.AttributeClass.Name.Contains("JsonA", StringComparison.CurrentCultureIgnoreCase))
                            {
                            ClassNames.Add(type.Name);
#if DEBUG
                            Console.WriteLine($"Added {type.Name} to ClassNames.");
#endif
                            break; // Stop processing attributes for this type
                            }
                        }
#if DEBUG
                    Console.WriteLine($"Checking type {type.ContainingNamespace}.{type.Name}");
#endif
                    }

                 void UpdateJSONHandler(List<string> taggedClasses)
                    {
                    
                    taggedClasses = taggedClasses.Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();

                    string jsonHandlerPath = "JSONHandler.cs";

                    List<string> AllPartialClasses = new List<string>();
                    List<string> AllDictionaryEntries = new List<string>();

                    foreach(string JSONAOTClass in taggedClasses)
                        {
                        string PartialClass = ($"\n    [JsonSerializable(typeof({JSONAOTClass}))]\n    public partial class {JSONAOTClass}Context : JsonSerializerContext {{ }}\n");
                        string DictionaryEntry = ($@"        {{ typeof({JSONAOTClass}), new {JSONAOTClass}Context().GetTypeInfo(typeof({JSONAOTClass})) }},");

                        AllPartialClasses.Add(PartialClass);
                        AllDictionaryEntries.Add(DictionaryEntry);
                        }

                    if(WRITEMODE == 0)
                        {
                        AppendToJSONHandler();
                        } else if (WRITEMODE == 1)
                        {
                        OverwriteToJSONHandler();
                        }

                        void OverwriteToJSONHandler()
                        {
                        string jsonHandlerTop = @"
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace JSONHandler
{
    public static class JSONHandler 
    {
        private static readonly Dictionary<Type, JsonTypeInfo> _serializerContexts = new  Dictionary<Type, JsonTypeInfo>(){
";

                        string jsonHandlerMiddle = @"
        };

        public static string Serialize<T>(T obj)
        {
            if (!_serializerContexts.TryGetValue(typeof(T), out var context))
            {
                throw new ArgumentException($""No serializer context found for type { typeof(T)}"");
            }

            return System.Text.Json.JsonSerializer.Serialize(obj, context);
        }

        public static T Deserialize<T>(string json)
            {
            if (!_serializerContexts.TryGetValue(typeof(T), out var context))
                {
                throw new ArgumentException($""No serializer context found for type {typeof(T)}"");
                }

            return System.Text.Json.JsonSerializer.Deserialize<T>(json, (JsonTypeInfo<T>)context);
            }
        }
#region ContextualizationOfClasses
    // space for adding context classes
";

                        
                        string jsonHandlerBottom = (@"
#endregion

    [AttributeUsage(AttributeTargets.Class)]
    public class JsonAotAttribute : Attribute { }
    }
");
                        StringBuilder entireJSONHandler = new StringBuilder(jsonHandlerTop);
                        foreach(string dictentry  in AllDictionaryEntries)
                            {
                            entireJSONHandler.AppendLine($"\t{dictentry}");
                            }
                        entireJSONHandler.Append(jsonHandlerMiddle);
                        foreach(string partialclass in AllPartialClasses)
                            {
                            entireJSONHandler.AppendLine($"{partialclass}");
                            }
                        entireJSONHandler.Append(jsonHandlerBottom);

                        bool errorEncountered = false;
                        try
                            {
                            File.Delete(jsonHandlerPath);
                            } catch
                            {
                            errorEncountered = true;
                            Console.WriteLine("Encountered error trying to remove old JSONHandler.cs\nAttempting to rewrite.");
                            }
                        try
                            {
                            File.WriteAllText(jsonHandlerPath, entireJSONHandler.ToString());
                            }
                        catch
                            {
                            errorEncountered = true;
                            } finally {
                            if(errorEncountered == false)
                                {
                                Console.WriteLine("JSONHandler.cs updated successfully!");
                                } else
                                {
                                Console.WriteLine("Errors were encountered during write process to JSONHandler.cs");
                                }
                            }
                        }

                        void AppendToJSONHandler()
                        {
                            // Read the existing JSONHandler content
                            List<string> jsonHandlerContent = File.ReadAllLines(jsonHandlerPath).ToList();

                            //new Dictionary<Type, JsonTypeInfo> {

                            // Find insertion points and update the content
                            int contextRegionStart = jsonHandlerContent
                                .Select((line, index) => new { Line = line, Index = index })
                                .FirstOrDefault(item => item.Line.Contains("#region ContextualizationOfClasses", StringComparison.CurrentCultureIgnoreCase))?
                                .Index ?? -1;

                            int dictionaryStart = jsonHandlerContent
                                .Select((line, index) => new { Line = line, Index = index })
                                .FirstOrDefault(item => item.Line.Contains("static readonly Dictionary<Type, JsonTypeInfo>", StringComparison.CurrentCultureIgnoreCase))?
                                .Index ?? -1;

                            // Insert partial classes first, as inserting into the dictionary first could alter the index of
                            // the region.

                            foreach (string partialclass in AllPartialClasses)
                                {
                                jsonHandlerContent.Insert(contextRegionStart + 1, partialclass);
                                }

                            foreach (string dictionaryentry in AllDictionaryEntries)
                                {
                                jsonHandlerContent.Insert(dictionaryStart + 1, '\t' + dictionaryentry);
                                }

                        bool errorEncountered = false;
                        try
                            {
                            // Write the modified content back to the file
                            File.WriteAllLines(jsonHandlerPath, jsonHandlerContent);
                            }
                        catch
                            {
                            errorEncountered = true;
                            }
                        finally
                            {
                            if (errorEncountered == false)
                                {
                                Console.WriteLine("JSONHandler.cs updated successfully!");
                                }
                            else
                                {
                                Console.WriteLine("Errors were encountered during write process to JSONHandler.cs");
                                }
                            }
                        }
                    }

                }
            }

        public static class JSONHandlerGenerator
            {
            public static void CreateJSONHandlerFile()
                {
                string jsonHandlerContent = @"
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace JSONHandler
{
    public static class JSONHandler 
    {
        private static readonly Dictionary<Type, JsonTypeInfo> _serializerContexts = new  Dictionary<Type, JsonTypeInfo>(){


        };

        public static string Serialize<T>(T obj)
        {
            if (!_serializerContexts.TryGetValue(typeof(T), out var context))
            {
                throw new ArgumentException($""No serializer context found for type { typeof(T)}"");
            }

            return System.Text.Json.JsonSerializer.Serialize(obj, context);
        }

        public static T Deserialize<T>(string json)
            {
            if (!_serializerContexts.TryGetValue(typeof(T), out var context))
                {
                throw new ArgumentException($""No serializer context found for type {typeof(T)}"");
                }

            return System.Text.Json.JsonSerializer.Deserialize<T>(json, (JsonTypeInfo<T>)context);
            }
        }
#region ContextualizationOfClasses
    // space for adding context classes

#endregion

    [AttributeUsage(AttributeTargets.Class)]
    public class JsonAotAttribute : Attribute { }
    }
";
                File.WriteAllText("JSONHandler.cs", jsonHandlerContent);
                Console.WriteLine("JSONHandler.cs created!");
                }
            public static void ModifyProgramCS()
                {
                string programCsPath = "Program.cs"; // Assume it's in the same directory

                if (!File.Exists(programCsPath))
                    {
                    Console.Error.WriteLine("Error: Program.cs not found.");
                    return;
                    }

                string[] programCsLines = File.ReadAllLines(programCsPath);
                using (StreamWriter writer = new StreamWriter(programCsPath))
                    {
                    writer.WriteLine("global using static JSONHandler.JSONHandler; // Streamline serialization and deserialization\r\nglobal using JSONHandler;"); // Insert at the top
                    foreach (string line in programCsLines)
                        {
                        writer.WriteLine(line);
                        }
                    }
                Console.WriteLine("Program.cs modified successfully!");
                }
            }
        }
    }