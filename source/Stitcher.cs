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
 * Specifically see  Program.cs
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace stitch_IL
{
    class Stitcher
    {
        public ModuleDefinition Module
        {
            get;
            private set;
        }

        public Stitcher(ModuleDefinition module)
        {
            this.Module = module;
        }

        public void AddMethodCallAtStart(string typeName, string methodName, MethodInfo patchMethod)
        {
            Console.WriteLine("Adding method call: {0} in type: {1}", patchMethod.Name, typeName);

            MethodDefinition method = new MethodDefinition(methodName, Mono.Cecil.MethodAttributes.Private, Module.TypeSystem.Void);
            Module.GetTypes().Where(t => t.Name.Equals(typeName)).FirstOrDefault().Methods.Add(method);
            method.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, Module.Import(patchMethod)));
            method.Body.Instructions.Insert(1, Instruction.Create(OpCodes.Ret));
        }

        public void AddPatchToType(string fabricTypeName, string fabricMethodName, MethodInfo patchMethod) // without "Patch" at end
        {
            // get the type
            TypeDefinition fabricType = Module.GetType(fabricTypeName);
            if(fabricType == null) {
                // didn't find the type, then look in nested classes for the type
                Console.WriteLine("Could not find {0}. Tries to search for nested classes.", fabricTypeName);
                fabricType = Module.GetTypes().Where(t => t.Name.Equals(fabricTypeName)).FirstOrDefault();
                if(fabricType != null) Console.WriteLine(" Found: " + fabricType.FullName);
            }

            if (fabricType != null)
            {
                // look for the method to patch in the target / fabric. And if specified number of args, then filter for that.
                bool foundMethodToPatch = false;
                foreach (MethodDefinition current in fabricType.Methods.Where(m => m.Name.Equals(fabricMethodName) || m.Name.Split('.').Last().Equals(fabricMethodName)))
                {
                    int numArgs = ((PatchInformationAttribute)patchMethod.GetCustomAttributes(true).First(a => a is PatchInformationAttribute)).TargetNumberOfArgs;
                    if (numArgs != -1) {
                        Console.WriteLine(" numArgs: " + numArgs + "    " + current.Parameters.Count);
                        if (current.Parameters.Count != numArgs) continue;
                    }
                    
                    // found the method, and adds the patch to the method
                    AddPatch(current, patchMethod);

                    foundMethodToPatch = true;
                    break;
                }
                if (!foundMethodToPatch)
                {
                    // print information about not finding the method and other methods found.
                    Console.WriteLine("(!) Cannot find any method matching {0}.{1}  methodname:{2}", fabricTypeName, fabricMethodName, fabricMethodName);
                    if (true) {
                        foreach (MethodDefinition current in fabricType.Methods)
                        {
                            Console.Write(current.Name + ", ");
                        }
                    }
                    Console.WriteLine();
                }
            }
            else {
                // could also search like fabricTypeName.EndsWith, and such but i'm not going to.
                Console.WriteLine("(!) There is no '{0}' type ", fabricTypeName);
            }
        }

        public void AddPatch(MethodDefinition fabricMethod, MethodInfo patchMethod)
        {
            // just some variable to keep track of what's happening.
            string stage = "unknown";
            
            if (!fabricMethod.HasBody)
            {
                Console.WriteLine("Cannot patch method '{0}' - does not have a body.", fabricMethod);
            }
            else { 
            
                try {
                if (fabricMethod.HasGenericParameters)
                {
                    throw new InvalidOperationException("Generic parameters not supported");
                }
                
                // default values for some attribute settings
                int offset = 0;
                int returnTo = 0;
                bool doReturn = false;
                bool doReturnIfNull = false;
                int storeloadResult = -1;
                int loadVar = -1;
                bool needArgs = true;
                bool needInstance = false;
                int neededArg = -1;
                bool insertDup = false;
                bool toReturn = false;

                List<Instruction> list = new List<Instruction>();

                // all methods passed should have an attribute, but we still check.
                    stage = "attribute"; 
                PatchInformationAttribute attribute = (PatchInformationAttribute)patchMethod.GetCustomAttributes(true).Where(a => a is PatchInformationAttribute).FirstOrDefault();
                if (attribute != null)
                {
                    Console.WriteLine("Stitching method '{0}' in '{1}'", patchMethod, fabricMethod);

                    // set the settings variables.
                    offset = attribute.IL_Offset;
                    returnTo = attribute.IL_ReturnTo;
                    doReturn = attribute.DoReturnAfterCall;
                    doReturnIfNull = attribute.DoReturnIfNullReturn;
                    storeloadResult = attribute.StoreLoadResult;
                    loadVar = attribute.LoadVar;
                    needArgs = attribute.PassOnAllArgs;
                    needInstance = attribute.PassInstanceArg;
                    neededArg = attribute.PassArg_ByIndex;
                    insertDup = attribute.InsertDupInstruction;
                    toReturn = attribute.DecideReturn;
                    
                    // if there is information that is clearly wrong, warn the user.
                    if (patchMethod.GetParameters().Length == 0 && needArgs)
                        Console.WriteLine("(!) patch method should have parameters. {0}() --> {0}(object[] args) - expect errors", patchMethod.Name);
                    if (offset > fabricMethod.Body.Instructions.Count)
                        Console.WriteLine("(!) Offset too big! in {0}   offset: {1}  |  instructions: {2}", fabricMethod.FullName, offset, fabricMethod.Body.Instructions.Count);
                            
                }
                else {
                    Console.WriteLine("(!) method without attribute! " + patchMethod.Name);
                    return;
                }

                // if there is a method of the same name but with suffix ShouldExecute, that method will determine whether the patch in question should execute.
                    stage = "finding ShouldExecute";
                MethodInfo shouldExe = patchMethod.DeclaringType.GetMethod(patchMethod.Name + "ShouldExecute", BindingFlags.Public | BindingFlags.Static);
                    stage = "start to add patch instructions";
                if (shouldExe != null) {
                    // if the ShouldExecute method has arguments, pass down some arguments.
                    if(shouldExe.GetParameters()?.Length == 1) {
                        list.Add(Instruction.Create(OpCodes.Ldarg_0));
                    }
                    else if(shouldExe.GetParameters()?.Length == 2) {
                        list.Add(Instruction.Create(OpCodes.Ldarg_0));
                        list.Add(Instruction.Create(OpCodes.Ldarg_1));
                    }
                    // call the method and insert an if to determine wheter the next part should be executed
                    list.Add(Instruction.Create(OpCodes.Call, Module.Import(shouldExe)));
                    list.Add(Instruction.Create(OpCodes.Brfalse, fabricMethod.Body.Instructions.ElementAt(offset)));

                    // if there is a second ShouldExecute  (this is bad code, but I haven't bothered to fix it)
                    // it is just repeating code
                    MethodInfo shouldExe2 = patchMethod.DeclaringType.GetMethod(patchMethod.Name + "ShouldExecute2", BindingFlags.Public | BindingFlags.Static);
                    if(shouldExe2 != null) {
                        Console.WriteLine("   should Exe: "+shouldExe?.ToString());
                        Console.WriteLine("   should Exe2: "+shouldExe2?.ToString());
                        Console.WriteLine("   should patch method: "+patchMethod.Name.ToString());
                       
                        if(shouldExe2.GetParameters()?.Length == 1) {
                            list.Add(Instruction.Create(OpCodes.Ldarg_0));
                        }
                        else if(shouldExe2.GetParameters()?.Length == 2) {
                            list.Add(Instruction.Create(OpCodes.Ldarg_0));
                            list.Add(Instruction.Create(OpCodes.Ldarg_1));
                        }
                        list.Add(Instruction.Create(OpCodes.Call, Module.Import(shouldExe2)));
                        list.Add(Instruction.Create(OpCodes.Brfalse, fabricMethod.Body.Instructions.ElementAt(offset)));
                    }
                }

                // if the settings for the patch says to insert a dup, then insert a dup
                if(insertDup) {
                    list.Add(Instruction.Create(OpCodes.Dup));
                }

                // pass on some argument?
                if(needInstance || neededArg != -1)
                {
                    stage = "needArg";
                    // if instance should be passed on, then pass it down.
                    if(needInstance) {
                        list.Add(Instruction.Create(OpCodes.Ldarg_0));
                    }
                    // if some specific arguments should be passed on, then do that.
                    if(neededArg != -1) {
                        // if the 8th bit is set, use the first bits as marker to what arguments to pass
                        if((neededArg & 256) != 0)
                        {
                            if((neededArg & 1) == 1)
                                list.Add(Instruction.Create(OpCodes.Ldarg_1));
                            if((neededArg & 2) == 2)
                                list.Add(Instruction.Create(OpCodes.Ldarg_2));
                            if((neededArg & 4) == 4)
                                list.Add(Instruction.Create(OpCodes.Ldarg_3));
                            if((neededArg & 8) == 8)
                                list.Add(Instruction.Create(OpCodes.Ldarg, fabricMethod.Parameters[4]));
                        }
                        else {
                            // else if the 8th bit is not set, then just use the number provided, as a index
                            OpCode op;
                            switch(neededArg)
                            {
                                case 0:
                                    op = OpCodes.Ldarg_0;
                                    break;
                                case 1:
                                    op = OpCodes.Ldarg_1;
                                    break;
                                case 2:
                                    op = OpCodes.Ldarg_2;
                                    break;
                                case 3:
                                    op = OpCodes.Ldarg_3;
                                    break;
                                default:
                                    // dude just use PassOnAllArgs
                                    stage = "wrong need arg, too big?";
                                    throw new Exception("Wrong NeededArg: " + patchMethod.Name);
                                break;
                            }
                            list.Add(Instruction.Create(op));
                        }
                    }
                }
                else if (needArgs) {
                    // this code makes an array filled with the method parameters
                        stage = "needArgs";
                    int countArgs = 0;
                    int argscount = fabricMethod.Parameters.Count + (fabricMethod.IsStatic ? 0 : 1);
                    list.Add(Instruction.Create(OpCodes.Ldc_I4, argscount));
                    list.Add(Instruction.Create(OpCodes.Newarr, Module.TypeSystem.Object));
                    
                    // if it is not static, then the first argument is not an paramenter, but actually the instance, but we want to pass that as well.
                    if (!fabricMethod.IsStatic) {
                        list.Add(Instruction.Create(OpCodes.Dup));
                        list.Add(Instruction.Create(OpCodes.Ldc_I4, 0));
                        list.Add(Instruction.Create(OpCodes.Ldarg_0));
                        list.Add(Instruction.Create(OpCodes.Stelem_Ref));
                        countArgs++;
                    }
                        stage = "needArgs + parameters   (try use object as parameter type?)";
                    foreach (ParameterDefinition current in fabricMethod.Parameters) {
                        list.Add(Instruction.Create(OpCodes.Dup));
                        list.Add(Instruction.Create(OpCodes.Ldc_I4, countArgs));
                        list.Add(Instruction.Create(OpCodes.Ldarg, current));
                        if (current.ParameterType.IsByReference) {
                            ByReferenceType byReferenceType = (ByReferenceType)current.ParameterType;
                            list.Add(Instruction.Create(OpCodes.Ldobj, byReferenceType.ElementType));
                            list.Add(Instruction.Create(OpCodes.Box, byReferenceType.ElementType));
                        }
                        else if (current.ParameterType.IsValueType) {
                            list.Add(Instruction.Create(OpCodes.Box, current.ParameterType));
                        }
                        list.Add(Instruction.Create(OpCodes.Stelem_Ref));
                        countArgs++;
                    }
                }

                // if it should load a specific variable
                if(loadVar != -1) {
                    list.Add(Instruction.Create(OpCodes.Ldloc, fabricMethod.Body.Variables.ElementAt(loadVar)));
                }
                    
                    stage = "add call to patch method";
                // call the patch
                list.Add(Instruction.Create(OpCodes.Call, Module.Import(patchMethod)));

                // if it should store the result in a variable, and then load that variable for the next function in line.
                if (storeloadResult != -1) {
                    list.Add(Instruction.Create(OpCodes.Stloc, fabricMethod.Body.Variables.ElementAt(storeloadResult)));
                    list.Add(Instruction.Create(OpCodes.Ldloc, fabricMethod.Body.Variables.ElementAt(storeloadResult)));
                }

                // if methods returns null, then the target method also returns.
                if(doReturnIfNull) {
                    list.Add(Instruction.Create(OpCodes.Dup));
                    list.Add(Instruction.Create(OpCodes.Ldnull));
                    list.Add(Instruction.Create(OpCodes.Ceq));
                    list.Add(Instruction.Create(OpCodes.Brfalse, fabricMethod.Body.Instructions.ElementAt(offset)));
                    if(!doReturn)list.Add(Instruction.Create(OpCodes.Pop));
                    list.Add(Instruction.Create(OpCodes.Ret));
                }
                // if it should return, add a return
                else if (doReturn)
                {
                        stage = "do return";
                    list.Add(Instruction.Create(OpCodes.Ret));
                }
                // if this method is used to determine whether to return or not
                else if (toReturn)
                {
                        stage = "ToReturn atr (voidReturn)...   ReturnTo might be too large, returnTo:"+ returnTo;
                    if(returnTo != -1)
                        list.Add(Instruction.Create(OpCodes.Brfalse, fabricMethod.Body.Instructions.ElementAt(returnTo)));
                    else
                        list.Add(Instruction.Create(OpCodes.Brfalse, fabricMethod.Body.Instructions.ElementAt(offset)));
                    list.Add(Instruction.Create(OpCodes.Ret));
                }
                // what IL code to return to after the method is called.
                else if (returnTo != -1)
                {
                        stage = "returnTo atr ..   ReturnTo might be too large, returnTo:" + returnTo;
                    list.Add(Instruction.Create(OpCodes.Br, fabricMethod.Body.Instructions.ElementAt(returnTo)));
                }
                    
                /*if(addBranchTo != -1)
                {
                        error = "addBranchTo atr ..   addBranchTo might be too large, addBranchTo:"+ addBranchTo;
                    list.Add(Instruction.Create(OpCodes.Br, method.Body.Instructions.ElementAt(addBranchTo)));
                }*/

                    stage = "on instruction order";
                list.Reverse();
                    stage = "insert instruction. offset:"+offset + "   count: "+fabricMethod.Body.Instructions.Count;
                foreach (Instruction current2 in list) {
                    fabricMethod.Body.Instructions.Insert(offset, current2); // insert (ilcode) call att the offset, normal is 0 (at the begining)
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("---------------------!");
                Console.WriteLine("error on AddPatch: "+stage);
                Console.WriteLine("method: {0} \tpatchMethod: {1}", fabricMethod, patchMethod);
                if(e is IndexOutOfRangeException) Console.WriteLine("IndexOutOfRangeException");
                Console.WriteLine(e);
                throw e; 
            }
            }
                
        } 
        

    }
}
