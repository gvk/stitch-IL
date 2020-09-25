/*
 * DO NOT ALTER OR REMOVE LICENSE NOTICES OR THIS FILE HEADER.
 *
 * This code is free software; you can redistribute it and/or modify it
 * under the terms of the GNU General Public License version 3 only, as
 * published by the Free Software Foundation.
 *
 * This code is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
 * FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * version 3 for more details (a copy is included in the LICENSE file that
 * accompanied this code).
 *
 * You should have received a copy of the GNU General Public License version 3
 * along with this work; if not, write to the Free Software Foundation.
 *
 * Please contact https://github.com/gvk if you need additional information or
 * have any questions.
 * 
 */

 /*
 * GitHub repository: https://github.com/gvk/stitch-IL
 * Project contains following files:  Program.cs,  Stitcher.cs,  PatchInformationAttribute.cs
 * See below for summary, if you don't want to read the documentation on github
 */

using System;
using System.Reflection;
using Mono.Cecil;
using System.IO;
using System.Linq;

namespace stitch_IL
{
    /// <summary>
    /// [ This entire program was written hastly, without much planing nore thinking. You may experience bugs and issues. Maybe even a headache or two while reading the code. ]
    /// (This program was not made when it was pushed to github, so libraries used might be outdated by several years)
    /// (I might return to fix this some other time, else feel free to fix it yourself)
    /// 
    /// This program takes two inputs first the target file to put the patch on ("the fabric"), and then the source file to patch with ("stitch" to "the fabric").
    /// It uses MonoCecil library to edit and save the target file, with stitches/hooks to the patch.
    /// You can easily modify the target, without causing much trouble.
    ///    
    /// MonoCecil can be found here: https://github.com/jbevain/cecil
    /// This project might be using an outdated version.
    /// </summary>
    class Program
    {

        // extention of the referenced libraries to load
        const string LIB_EXT = ".dll";

        // suffix to search for in type names in patch file.
        const string SUFFIX = "Patch";

        // some types and methods contain the symbol '<' in the name, so patching methods will have to use a replacement for that character.
        const string LESSER_THAN_CHARACTER_REPLACEMENT = "_LT_";
        // some types and methods contain the symbol '>' in the name, so patching methods will have to use a replacement for that character.
        const string GREATER_THAN_CHARACTER_REPLACEMENT = "_GT_"; // can be used like so:  <>__AnonType0  -->  _LT__GT___AnonType0  -->  _LT__GT___AnonType0Patch

        // multiple patches/hooks from the same class may result in methods with the same name, which is not possible,
        //  so this marker will be ignored when patching so methods can be differentiated
        const string METHOD_DIFFERENTIATE_SYMBOL = "II"; // can be used like so:  func(),  funcII(),  funcIIII()

        static void Main(string[] args)
        {
            // setup folder for any libraries used by the edited file. (the file being edited might need to be a library as well)
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += new ResolveEventHandler(LoadFromFolder);

            // file to edit  (fabric to stitch a patch on)
            string inputFabricFileName = args[0];
            // file to edit  (the patch to stitch on the fabric)
            string inputPatchFileName = args[1];
            
            //OutputLogToFile() // nah.

            DoJob(inputFabricFileName, inputPatchFileName);

            
            // program end
            Console.Out.Close();
            Console.ReadKey();
        }

        static void DoJob(string inputFabricFileName, string inputPatchFileName)
        {
            Console.WriteLine("Sitching file: " + inputFabricFileName);
            Console.WriteLine("with patch: " + inputPatchFileName);

            FileStream stream = File.Open(".\\" + inputFabricFileName, FileMode.Open, FileAccess.ReadWrite);
            AssemblyDefinition fabricAssemblyDefinition = AssemblyDefinition.ReadAssembly(stream);

            Stitcher stitcher = new Stitcher(fabricAssemblyDefinition.MainModule);
            Assembly patchAssembly = Assembly.LoadFrom(inputPatchFileName);

            Type[] types = LoadTypesInPatch(patchAssembly);
            
            // Get all 'public static' methods in classes with Patch at the end of the name
            types.Where(t => t.Name.Length > SUFFIX.Length+1 && t.Name.Substring(t.Name.Length - SUFFIX.Length).Equals(SUFFIX)).Select(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)).ToList().ForEach(methods => {
                foreach (MethodInfo method in methods.Where(m => !m.IsFamily && m.GetCustomAttributes(true).FirstOrDefault(a => a is PatchInformationAttribute) != null)) // methods in ***Patch class
                {
                    string typeName = method.ReflectedType.Name;
                    
                    // remove the "Patch" part of the name
                    typeName = typeName.Remove(typeName.Length - SUFFIX.Length);
                    // fix names with angle brackets < >
                    typeName = FixNamesWithAngleBrackets(typeName);
                    // the type name now resemmbles the one in the fabric / target to edit.

                    string methodName = method.Name;
                    // fix names that appear multiple times, i.e. method that is patched/hooked multiple times by the same class.
                    methodName = methodName.Replace(METHOD_DIFFERENTIATE_SYMBOL, "");
                    // fix names with angle brackets < >
                    methodName = FixNamesWithAngleBrackets(methodName);
                    // the method name now resemmbles the one in the fabric / target to edit.
                    
                    // fix constructors / static code names
                    if (methodName.Equals("ctor")) methodName = ".ctor";
                    if (methodName.Equals("cctor")) methodName = ".cctor";

                    // finally run the stitcher to add the patches/hook from the suffixed class to the method in the target.
                    // target type name, target method name, source method/patch to apply
                    stitcher.AddPatchToType(typeName, methodName, method);
                }});


            // WRITE
            // edit the version
            fabricAssemblyDefinition.Name.Version = new System.Version(99, 99, 99, 99);
            // output the file
            fabricAssemblyDefinition.Write(inputFabricFileName.Remove(inputFabricFileName.Length - LIB_EXT.Length) + ".out.dll");
            //stream.Close(); // was it already closed?, or why did I comment this?
            Console.WriteLine("Finnished stitching of " + inputFabricFileName);
        }

        static string FixNamesWithAngleBrackets(string name)
        {
            // if it contains the symbols we are looking for, replace and print the new name
            if (name.Contains(GREATER_THAN_CHARACTER_REPLACEMENT) || name.Contains(LESSER_THAN_CHARACTER_REPLACEMENT)) {
                name = name.Replace(LESSER_THAN_CHARACTER_REPLACEMENT, "<");
                name = name.Replace(GREATER_THAN_CHARACTER_REPLACEMENT, ">");
                Console.WriteLine("new type/method name: " + name);
            }
            return name;
        }

        static Type[] LoadTypesInPatch(Assembly patchAssembly)
        {
            // Bad way to solve a crash with certain files, that I haven't been bothered to investigated yet.
            Type[] types = null;
            try {
                types = patchAssembly.GetTypes();
                Console.WriteLine("Number of found types in patch: " + types.Length);
            } catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine("An error has occured while loading the patch. This is usually fine though.");
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.Source);
                Type[] ts = ex.Types;
                Console.WriteLine("Types:");
                for(int i = 0; i < ts?.Length; i++) {
                    if (ts[i] != null)
                         Console.WriteLine(ts[i].ToString());
                    else Console.WriteLine(i + ": null");
                }
            }
            //return types;
            return patchAssembly.GetTypes();
        }

        static Assembly LoadFromFolder(object sender, ResolveEventArgs args) {
            // get the folder we have put refernce libraries in, and load and return them to the loader.

            string exeFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            string referencesFolder = Path.Combine(exeFolder, "references");

            if(!Directory.Exists(referencesFolder)) {
                Directory.CreateDirectory(referencesFolder);
            }
            
            string assemblyPath = Path.Combine(referencesFolder, new AssemblyName(args.Name).Name + LIB_EXT);
            
            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            Console.WriteLine("loaded assemlby: "+assemblyPath);

            return assembly;
        }

        static void OutputLogToFile()
        {
            // sets the Console output to "out.txt"
            StreamWriter streamwriter = new StreamWriter(new FileStream("out.txt", FileMode.Create));
            streamwriter.AutoFlush = true;
            Console.SetOut(streamwriter);
            Console.SetError(streamwriter);
        }

    }
}
