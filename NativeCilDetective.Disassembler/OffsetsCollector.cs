﻿using Mono.Cecil;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NativeCilDetective.Disassembler
{
    public class OffsetsCollector
    {
        class StringOffset
        {
            public string Value { get; set; }
            public string Address { get; set; }
        }

        public string Il2cppDumpPath { get; }

        public IReadOnlyCollection<AssemblyDefinition> Assemblies { get => assemblies.AsReadOnly(); }
        private List<AssemblyDefinition> assemblies;

        public IReadOnlyCollection<MethodDefinition> Methods { get => methods.AsReadOnly(); }
        private List<MethodDefinition> methods;

        public IDictionary<long, string> StringsFromOffsets { get; private set; }

        public IDictionary<long, MethodDefinition> MethodsFromOffsets { get; private set; }

        public OffsetsCollector(string il2cppDumpPath)
        {
            Il2cppDumpPath = il2cppDumpPath;
        }

        public void ReadStrings()
        {
            StringsFromOffsets = new Dictionary<long, string>();
            string jsonfile = Path.Combine(Il2cppDumpPath, "stringliteral.json");

            if (!File.Exists(jsonfile))
            {
                throw new Exception($"Could not find stringliteral.json in '{Il2cppDumpPath}'.");
            }

            var offsets = JsonConvert.DeserializeObject<StringOffset[]>(File.ReadAllText(jsonfile));

            foreach (var offset in offsets)
            {
                // Subtracting 0xC00 is not required on the MacOS builds?
                StringsFromOffsets.Add(long.Parse(offset.Address.Replace("0x", ""), NumberStyles.HexNumber) - 0xC00, offset.Value);
            }
        }

        public void ReadMethods()
        {
            string folder = Path.Combine(Il2cppDumpPath, "DummyDll");
            if (!Directory.Exists(folder))
            {
                throw new Exception($"Could not the DummyDll folder in '{Il2cppDumpPath}'.");
            }

            MethodsFromOffsets = new Dictionary<long, MethodDefinition>();
            assemblies = new List<AssemblyDefinition>();
            methods = new List<MethodDefinition>();
            foreach (string file in Directory.GetFiles(folder))
            {
                if (file.ToUpper().EndsWith(".DLL"))
                {
                    using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(file))
                    {
                        AnalyzeAssembly(assembly);
                        assemblies.Add(assembly);
                    }
                }
            }
        }

        private void AnalyzeAssembly(AssemblyDefinition assembly)
        {
            foreach (ModuleDefinition module in assembly.Modules)
            {
                AnalyzeModule(module);
            }
        }

        private void AnalyzeModule(ModuleDefinition module)
        {
            foreach (TypeDefinition type in module.Types)
            {
                AnalyzeType(type);
            }
        }

        private void AnalyzeType(TypeDefinition type)
        {
            foreach (MethodDefinition method in type.Methods)
            {
                AnalyzeMethod(method);
            }
        }

        private void AnalyzeMethod(MethodDefinition method)
        {
            methods.Add(method);

            long offset = GetMethodOffset(method);
            if (offset != -1 && !MethodsFromOffsets.ContainsKey(offset))
            {
                MethodsFromOffsets.Add(offset, method);
            }
        }

        public static long GetMethodOffset(MethodDefinition method)
        {
            var addressAttribute = method.CustomAttributes.FirstOrDefault(x => x.AttributeType.Name == "AddressAttribute");
            if (addressAttribute != null)
            {
                var offsetField = addressAttribute.Fields.First(x => x.Name == "Offset");
                long offsetValue = long.Parse(((string)offsetField.Argument.Value).Replace("0x", ""), NumberStyles.HexNumber);

                return offsetValue;
            }
            return -1;
        }
    }
}
