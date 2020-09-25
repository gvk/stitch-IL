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

namespace stitch_IL
{
    [AttributeUsage(AttributeTargets.Method)]
    public class PatchInformationAttribute : Attribute
    {
        private int offset = 0; 
        public int IL_Offset  // instruction index to insert patch at
        {
            get
            {
                return offset;
            }
            set
            {
                offset = value;
            }
        }

        private int returnTo = -1; // instruction index to return to after patch
        public int IL_ReturnTo
        {
            get
            {
                return returnTo;
            }
            set
            {
                returnTo = value;
            }
        }

        private bool ret;
        public bool DoReturnAfterCall // whether method should return after patch
        {
            get
            {
                return ret;
            }
            set
            {
                ret = value;
            }
        }
        private bool retNull;
        public bool DoReturnIfNullReturn // whether method should return if patch returns null
        {
            get
            {
                return retNull;
            }
            set
            {
                retNull = value;
            }
        }

        private int storeResult = -1;
        public int StoreLoadResult // whether to store and load the result of the patch in variable, it will push the variable after wards
        {
            get
            {
                return storeResult;
            }
            set
            {
                storeResult = value;
            }
        }
        private int loadVar = -1;
        public int LoadVar // whether to load a variable before calling the patch, effectivly passing the variable to the patch.
        {
            get
            {
                return loadVar;
            }
            set
            {
                loadVar = value;
            }
        }

        private bool passOnArgs = false;
        public bool PassOnAllArgs // whether to pass on all arguments of the method to the patch
        {
            get
            {
                return passOnArgs;
            }
            set
            {
                passOnArgs = value;
            }
        }
        private bool passInstance = false;
        public bool PassInstanceArg // whether to pass on the instance (aka "this") to the patch
        {
            get
            {
                return passInstance;
            }
            set
            {
                passInstance = value;
            }
        }
        private int passingArg_byIndex = -1;
        public int PassArg_ByIndex // pass specific argument by its index
        {
            get
            {
                return passingArg_byIndex;
            }
            set
            {
                passingArg_byIndex = value;
            }
        }
        private bool insertDup = false;
        public bool InsertDupInstruction // insert dup instruction before calling the patch
        {
            get
            {
                return insertDup;
            }
            set
            {
                insertDup = value;
            }
        }

        private bool toRet;
        public bool DecideReturn // used to say that this method determines whether to return or not
        {
            get
            {
                return toRet;
            }
            set
            {
                toRet = value;
            }
        }

        private int numOfArgs = -1;
        public int TargetNumberOfArgs // what number of args the target method has.
        {
            get
            {
                return numOfArgs;
            }
            set
            {
                numOfArgs = value;
            }
        }

    }
}
