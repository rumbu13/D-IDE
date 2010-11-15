﻿using System;
using System.Collections.Generic;
using System.Text;

namespace D_Parser
{
    /// <summary>
    /// Generic class for resolve module relations and/or declarations
    /// </summary>
    public class DCodeResolver
    {
        #region Direct Code search
        public static TypeDeclaration BuildIdentifierList(string Text, int CaretOffset, bool BackwardOnly, out DToken OptionalInitToken)
        {
            OptionalInitToken = null;

            #region Step 1: Walk along the code to find the declaration's beginning
            if (String.IsNullOrEmpty(Text) || CaretOffset >= Text.Length) return null;
            // At first we only want to find the beginning of our identifier list
            // later we will pass the text beyond the beginning to the parser - there we parse all needed expressions from it
            int IdentListStart = -1;

            /*
            T!(...)>.<
             */

            int isComment = 0;
            bool isString = false, expectDot = false, hadDot = true;
            var bracketStack = new Stack<char>();
            bool stopSeeking = false;

            for (int i = CaretOffset; i >= 0 && !stopSeeking; i--)
            {
                IdentListStart = i;
                var c = Text[i];
                var str = Text.Substring(i);
                char p = ' ';
                if (i > 0) p = Text[i - 1];

                // Primitive comment check
                if (!isString && c == '/' && (p == '*' || p == '+'))
                    isComment++;
                if (!isString && isComment > 0 && (c == '+' || c == '*') && p == '/')
                    isComment--;

                // Primitive string check
                //TODO: "blah">.<
                if (isComment < 1 && c == '"' && p != '\\')
                    isString = !isString;

                // If string or comment, just continue
                if (isString || isComment > 0)
                    continue;

                // If between brackets, skip
                if (bracketStack.Count > 0 && c != bracketStack.Peek())
                    continue;

                // Bracket check
                switch (c)
                {
                    case ']':
                        bracketStack.Push('[');
                        continue;
                    case ')':
                        bracketStack.Push('(');
                        continue;
                    case '}':
                        bracketStack.Push('{');
                        continue;

                    case '[':
                    case '(':
                    case '{':
                        if (bracketStack.Count > 0 && bracketStack.Peek() == c)
                        {
                            bracketStack.Pop();
                            if (p == '!') // Skip template stuff
                                i--;
                        }
                        else
                        {
                            // Stop if we reached the most left existing bracket
                            // e.g. foo>(< bar| )
                            stopSeeking = true;
                            IdentListStart++;
                        }
                        continue;
                }

                // whitespace check
                if (Char.IsWhiteSpace(c)) { if (hadDot) expectDot = false; else expectDot = true; continue; }

                if (c == '.')
                {
                    expectDot = false;
                    hadDot = true;
                    continue;
                }

                /*
                 * abc
                 * abc . abc
                 * T!().abc[]
                 * def abc.T
                 */
                if (Char.IsLetterOrDigit(c) || c == '_')
                {
                    hadDot = false;

                    if (!expectDot)
                        continue;
                    else
                        IdentListStart++;
                }

                stopSeeking = true;
            }
            #endregion

            #region 2: Init the parser
            if (!stopSeeking || IdentListStart < 0)
                return null;

            // If code e.g. starts with a bracket, increment IdentListStart
            var ch = Text[IdentListStart];
            if (!Char.IsLetterOrDigit(ch) && ch != '_' && ch != '.')
                IdentListStart++;

            var psr = DParser.ParseBasicType(BackwardOnly ? Text.Substring(IdentListStart, CaretOffset - IdentListStart) : Text.Substring(IdentListStart),out OptionalInitToken);
            #endregion

            return psr;
        }

        public static DBlockStatement SearchBlockAt(DBlockStatement Parent, CodeLocation Where)
        {
            foreach (var n in Parent)
            {
                if (!(n is DBlockStatement)) continue;

                var b = n as DBlockStatement;
                if (Where > b.BlockStartLocation && Where < b.EndLocation)
                    return SearchBlockAt(b, Where);
            }

            return Parent;
        }

        public static DBlockStatement SearchClassLikeAt(DBlockStatement Parent, CodeLocation Where)
        {
            foreach (var n in Parent)
            {
                if (!(n is DClassLike)) continue;

                var b = n as DBlockStatement;
                if (Where > b.BlockStartLocation && Where < b.EndLocation)
                    return SearchClassLikeAt(b, Where);
            }

            return Parent;
        }
        #endregion

        #region Import path resolving
        /// <summary>
        /// Returns all imports of a module and those public ones of the imported modules
        /// </summary>
        /// <param name="cc"></param>
        /// <param name="ActualModule"></param>
        /// <returns></returns>
        public static List<DModule> ResolveImports(List<DModule> CodeCache, DModule ActualModule)
        {
            var ret = new List<DModule>();
            if (CodeCache == null || ActualModule == null) return ret;

            // First add all local imports
            var localImps = new List<string>();
            foreach (var kv in ActualModule.Imports)
                localImps.Add(kv.Key);

            foreach (var m in CodeCache)
                if (localImps.Contains(m.ModuleName) && !ret.Contains(m))
                {
                    ret.Add(m);
                    /* 
                     * dmd-feature: public imports only affect the directly superior module
                     *
                     * Module A:
                     * import B;
                     * 
                     * foo(); // Will fail, because foo wasn't found
                     * 
                     * Module B:
                     * import C;
                     * 
                     * Module C:
                     * public import D;
                     * 
                     * Module D:
                     * void foo() {}
                     * 
                     * 
                     * Whereas
                     * Module B:
                     * public import C;
                     * 
                     * will succeed because we have a closed import hierarchy in which all imports are public.
                     * 
                     */

                    // So if there aren't any public imports in our import, continue without doing anything
                    ResolveImports(ref ret, CodeCache, m);
                }

            return ret;
        }

        public static void ResolveImports(ref List<DModule> ImportModules, List<DModule> CodeCache, DModule ActualModule)
        {
            var localImps = new List<string>();
            foreach (var kv in ActualModule.Imports)
                if (kv.Value)
                    localImps.Add(kv.Key);

            foreach (var m in CodeCache)
                if (localImps.Contains(m.ModuleName) && !ImportModules.Contains(m))
                {
                    ImportModules.Add(m);
                    ResolveImports(ref ImportModules, CodeCache, m);
                }
        }
        #endregion

        #region Declaration resolving

        public static DModule SearchModuleInCache(List<DModule> HayStack,string ModuleName)
        {
            foreach (var m in HayStack)
            {
                if (m.ModuleName == ModuleName) return m;
            }
            return null;
        }

        /// <summary>
        /// Finds the location (module node) where a type (TypeExpression) has been declared.
        /// Note: This function only searches within 1 module only!
        /// </summary>
        /// <param name="Module"></param>
        /// <param name="IdentifierList"></param>
        /// <returns>When a type was found, the declaration entry will be returned. Otherwise, it'll return null.</returns>
        public static DNode[] ResolveTypeDeclarations_ModuleOnly(List<DModule> ImportCache,DBlockStatement BlockNode, TypeDeclaration IdentifierList)
        {
            var ret = new List<DNode>();

            if (BlockNode == null || IdentifierList == null) return ret.ToArray();

            // Modules    Declaration
            // |---------|-----|
            // std.stdio.writeln();
            if (IdentifierList is IdentifierList)
            {
                var il = IdentifierList as IdentifierList;
                var skippedIds = 0;

                // Now search the entire block
                var istr = il.ToString();

                var mod = BlockNode.NodeRoot as DModule;
                /* If the id list start with the name of BlockNode's root module, 
                 * skip those first identifiers to proceed seeking the rest of the list
                 */
                if (mod != null && istr.StartsWith(mod.ModuleName))
                {
                    skippedIds += mod.ModuleName.Split('.').Length;
                    istr = il.ToString(skippedIds);
                }

                // Now move stepwise deeper calling ResolveTypeDeclaration recursively
                ret. Add(BlockNode);

                while (skippedIds < il.Parts.Count && ret.Count>0)
                {
                    var DeeperLevel = new List<DNode>();
                    // As long as our node can contain other nodes, scan it
                    foreach(var n in ret)
                        DeeperLevel.AddRange( ResolveTypeDeclarations_ModuleOnly(ImportCache,n as DBlockStatement, il[skippedIds]));
                    skippedIds++;
                    ret = DeeperLevel;
                }

                return ret.ToArray();
            }

            //HACK: Scan the type declaration list for any NormalDeclarations
            var td = IdentifierList;
            while (td != null && !(td is NormalDeclaration))
                td = td.Base;

            if (td is NormalDeclaration)
            {
                var nameIdent = td as NormalDeclaration;

                // Scan from the inner to the outer level
                var currentParent = BlockNode;
                while (currentParent != null)
                {
                    // Scan the node's children for a match - return if we found one
                    foreach (var ch in currentParent)
                    {
                        if (nameIdent.Name == ch.Name)
                            ret.Add(ch);
                    }

                    // If our current Level node is a class-like, also attempt to parse its baseclass
                    if (currentParent is DClassLike)
                    {
                        var baseClass = ResolveBaseClass(ImportCache,currentParent as DClassLike);
                        if(baseClass!=null)
                            ret.AddRange( ResolveTypeDeclarations_ModuleOnly(ImportCache, baseClass, nameIdent));
                    }

                    // Move root-ward
                    currentParent = currentParent.Parent as DBlockStatement;
                }
            }

            //TODO: Here a lot of additional checks and more detailed type evaluations are missing!

            return ret.ToArray();
        }

        /// <summary>
        /// Search a type within an entire Module Cache.
        /// </summary>
        /// <param name="ImportCache"></param>
        /// <param name="CurrentlyScopedBlock"></param>
        /// <param name="IdentifierList"></param>
        /// <returns></returns>
        public static DNode[] ResolveTypeDeclarations(List<DModule> ImportCache, DBlockStatement CurrentlyScopedBlock, TypeDeclaration IdentifierList)
        {
            var ret = new List<DNode>();

            var ThisModule = CurrentlyScopedBlock.NodeRoot as DModule;

            // Of course it's needed to scan our own module at first
            if (ThisModule != null)
                ret.AddRange(ResolveTypeDeclarations_ModuleOnly(ImportCache,CurrentlyScopedBlock, IdentifierList));

            // Important: Implicitly add the object module
            /*var objmod = SearchModuleInCache(ModuleCache, "object");
            if (!LookupModules.Contains(objmod))
                LookupModules.Add(objmod);*/

            // Then search within the imports for our IdentifierList
            foreach (var m in ImportCache)
                if(m.ModuleFileName!=ThisModule.ModuleFileName) // We already parsed this module
                    ret.AddRange( ResolveTypeDeclarations_ModuleOnly(ImportCache,m, IdentifierList));

            return ret.ToArray();
        }

        /// <summary>
        /// Combines LocalModules and GlobalModules to one array and then search a type in it.
        /// Note: LocalModules will be searched first!
        /// </summary>
        /// <param name="ImportModules"></param>
        /// <param name="LocalModules"></param>
        /// <param name="CurrentlyScopedBlock"></param>
        /// <param name="IdentifierList"></param>
        /// <returns></returns>
        public static DNode[] ResolveTypeDeclarations(List<DModule> ImportModules, List<DModule> LocalModules, DBlockStatement CurrentlyScopedBlock, TypeDeclaration IdentifierList)
        {
            var SearchArea = new List<DModule>(LocalModules);
            SearchArea.AddRange(ImportModules);

            return ResolveTypeDeclarations(SearchArea, CurrentlyScopedBlock, IdentifierList);
        }

        /// <summary>
        /// Resolves all base classes of a class, struct, template or interface
        /// </summary>
        /// <param name="ModuleCache"></param>
        /// <param name="ActualClass"></param>
        /// <returns></returns>
        public static DClassLike ResolveBaseClass(List<DModule> ModuleCache,DClassLike ActualClass)
        {
            // Implicitly set the object class to the inherited class if no explicit one was done
            if (ActualClass.BaseClasses.Count < 1)
            {
                var ObjectClass = ResolveTypeDeclarations(ModuleCache, ActualClass.NodeRoot as DBlockStatement, new NormalDeclaration("Object"));
                if (ObjectClass.Length > 0 && ObjectClass[0] != ActualClass) // Yes, it can be null - like the Object class which can't inherit itself
                    return ObjectClass[0] as DClassLike;
            }
            else // Take the first only (since D forces single inheritance)
            {
                var ClassMatches = ResolveTypeDeclarations(ModuleCache, ActualClass.NodeRoot as DBlockStatement, ActualClass.BaseClasses[0]);
                if (ClassMatches.Length > 0)
                    return ClassMatches[0] as DClassLike;
            }

            return null;
        }
        
        #endregion

        /// <summary>
        /// Trivial class which cares about locating Comments and other non-code blocks within a code file
        /// </summary>
        public class Commenting
        {
            public static int IndexOf(string HayStack, bool Nested, int Start)
            {
                string Needle = Nested ? "+/" : "*/";
                char cur = '\0';
                int off = Start;
                bool IsInString = false;
                int block = 0, nested = 0;

                while (off < HayStack.Length)
                {
                    cur = HayStack[off];

                    // String check
                    if (cur == '\"' && (off < 1 || HayStack[off - 1] != '\\'))
                    {
                        IsInString = !IsInString;
                    }

                    if (!IsInString && (cur == '/') && (HayStack[off + 1] == '*' || HayStack[off + 1] == '+'))
                    {
                        if (HayStack[off + 1] == '*')
                            block++;
                        else
                            nested++;

                        off += 2;
                        continue;
                    }

                    if (!IsInString && cur == Needle[0])
                    {
                        if (off + Needle.Length >= HayStack.Length)
                            return -1;

                        if (HayStack.Substring(off, Needle.Length) == Needle)
                        {
                            if (Nested) nested--; else block--;

                            if ((Nested ? nested : block) < 0) // that value has to be -1 because we started to count at 0
                                return off;

                            off++; // Skip + or *
                        }

                        if (HayStack.Substring(off, 2) == (Nested ? "*/" : "+/"))
                        {
                            if (Nested) block--; else nested--;
                            off++;
                        }
                    }

                    off++;
                }
                return -1;
            }

            public static int LastIndexOf(string HayStack, bool Nested, int Start)
            {
                string Needle = Nested ? "/+" : "/*";
                char cur = '\0', prev = '\0';
                int off = Start;
                bool IsInString = false;
                int block = 0, nested = 0;

                while (off >= 0)
                {
                    cur = HayStack[off];
                    if (off > 0) prev = HayStack[off - 1];

                    // String check
                    if (cur == '\"' && (off < 1 || HayStack[off - 1] != '\\'))
                    {
                        IsInString = !IsInString;
                    }

                    if (!IsInString && (cur == '+' || cur == '*') && HayStack[off + 1] == '/')
                    {
                        if (cur == '*')
                            block--;
                        else
                            nested--;

                        off -= 2;
                        continue;
                    }

                    if (!IsInString && cur == '/')
                    {
                        if (HayStack.Substring(off, Needle.Length) == Needle)
                        {
                            if (Nested) nested++; else block++;

                            if ((Nested ? nested : block) >= 1)
                                return off;
                        }

                        if (HayStack.Substring(off, 2) == (Nested ? "/*" : "/+"))
                        {
                            if (Nested) block++; else nested++;
                            off--;
                        }
                    }

                    off--;
                }
                return -1;
            }

            public static void IsInCommentAreaOrString(string Text, int Offset, out bool IsInString, out bool IsInLineComment, out bool IsInBlockComment, out bool IsInNestedBlockComment)
            {
                char cur = '\0', peekChar = '\0';
                int off = 0;
                IsInString = IsInLineComment = IsInBlockComment = IsInNestedBlockComment = false;

                while (off < Offset - 1)
                {
                    cur = Text[off];
                    if (off < Text.Length - 1) peekChar = Text[off + 1];

                    // String check
                    if (!IsInLineComment && !IsInBlockComment && !IsInNestedBlockComment && cur == '\"' && (off < 1 || Text[off - 1] != '\\'))
                        IsInString = !IsInString;

                    if (!IsInString)
                    {
                        // Line comment check
                        if (!IsInBlockComment && !IsInNestedBlockComment)
                        {
                            if (cur == '/' && peekChar == '/')
                                IsInLineComment = true;
                            if (IsInLineComment && cur == '\n')
                                IsInLineComment = false;
                        }

                        // Block comment check
                        if (cur == '/' && peekChar == '*')
                            IsInBlockComment = true;
                        if (IsInBlockComment && cur == '*' && peekChar == '/')
                            IsInBlockComment = false;

                        // Nested comment check
                        if (!IsInString && cur == '/' && peekChar == '+')
                            IsInNestedBlockComment = true;
                        if (IsInNestedBlockComment && cur == '+' && peekChar == '/')
                            IsInNestedBlockComment = false;
                    }

                    off++;
                }
            }

            public static bool IsInCommentAreaOrString(string Text, int Offset)
            {
                bool a, b, c, d;
                IsInCommentAreaOrString(Text, Offset, out a, out b, out c, out d);

                return a || b || c || d;
            }
        }
    }
}