﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace D_Parser
{
    /// <summary>
    /// Parser for D Code
    /// </summary>
    public partial class DParser
    {
        public string PhysFileName;

        public static CodeLocation GetCodeLocation(DToken t) { return new CodeLocation(t.Location.Column, t.Location.Line); }
        public static CodeLocation ToCodeEndLocation(DToken t) { return new CodeLocation(t.EndLocation.Column, t.EndLocation.Line); }

        /// <summary>
        /// Parses D source file
        /// </summary>
        /// <param name="fn"></param>
        /// <param name="imports"></param>
        /// <param name="folds"></param>
        /// <returns>Module structure</returns>
        public static DNode ParseFile(string moduleName, string fn, out List<string> imports)
        {
            if (!File.Exists(fn)) { imports = new List<string>(); return null; }
            DNode ret = new DNode(FieldType.Root);

            FileStream fs;
            try
            {
                fs = new FileStream(fn, FileMode.Open);
            }
            catch (IOException iox) { imports = new List<string>(); return null; }
            TextReader tr = new StreamReader(fs);

            DLexer dl = new DLexer(tr);

            DParser p = new DParser(dl);
            //dl.Errors.SemErr = p.SemErr;
            //dl.Errors.SynErr = p.SynErr;
            p.PhysFileName = fn;
            //p.SemErr(DTokens.Short);
            if (fs.Length > (1024 * 1024 * 2))
            {
                OnError(fn, moduleName, 0, 0, DTokens.EOF, "DParser only parses files that are smaller than 2 MBytes!");
                imports = new List<string>();
                return ret;
            }

            ret = p.Parse(moduleName, out imports);

            fs.Close();


            return ret;
        }

        /// <summary>
        /// Parses D source text.
        /// See also <seealso cref="ParseFile"/>
        /// </summary>
        /// <param name="cont"></param>
        /// <param name="imports"></param>
        /// <param name="folds"></param>
        /// <returns>Module structure</returns>
        public static DNode ParseText(string file, string moduleName, string cont, out List<string> imports)
        {
            if (cont == null || cont.Length < 1) { imports = null; return null; }
            DNode ret = new DNode(FieldType.Root);

            TextReader tr = new StringReader(cont);

            DParser p = new DParser(new DLexer(tr));
            p.PhysFileName = file;
            if (cont.Length > (1024 * 1024 * 2))
            {
                p.SemErr(DTokens.EOF, 0, 0, "DParser only parses files that are smaller than 2 MBytes!");
                imports = new List<string>();
                return ret;
            }
            ret = p.Parse(moduleName, out imports);
            tr.Close();

            return ret;
        }

        public static DParser Create(TextReader tr)
        {
            DLexer dl = new DLexer(tr);
            return new DParser(dl);
        }

        /// <summary>
        /// Encapsules whole document structure
        /// </summary>
        DNode doc;

        public List<string> import;

        /// <summary>
        /// Modifiers for entire block
        /// </summary>
        List<int> BlockModifiers;
        /// <summary>
        /// Modifiers for current expression only
        /// </summary>
        List<int> ExpressionModifiers;

        public DNode Document
        {
            get { return doc; }
        }

        public string SkipToSemicolon()
        {
            string ret = "";

            int mbrace = 0, par = 0;
            while (la.Kind != DTokens.EOF)
            {
                if (la.Kind == DTokens.OpenCurlyBrace) mbrace++;
                if (la.Kind == DTokens.CloseCurlyBrace) mbrace--;

                if (la.Kind == DTokens.OpenParenthesis) par++;
                if (la.Kind == DTokens.CloseParenthesis) par--;

                if (ThrowIfEOF(DTokens.Semicolon)) break;
                if (mbrace < 1 && par < 1 && la.Kind != DTokens.Semicolon && Peek(1).Kind == DTokens.CloseCurlyBrace)
                {
                    ret += strVal;
                    SynErr(la.Kind, "Check for missing semicolon!");
                    break;
                }
                if (mbrace < 1 && par < 1 && la.Kind == DTokens.Semicolon)
                {
                    break;
                }
                if (ret.Length < 2000) ret += strVal;
                lexer.NextToken();
            }
            return ret;
        }

        public void SkipToClosingBrace()
        {
            int mbrace = 0;
            while (la.Kind != DTokens.EOF)
            {
                if (ThrowIfEOF(DTokens.CloseCurlyBrace)) return;
                if (la.Kind == DTokens.OpenCurlyBrace)
                {
                    mbrace++;
                }
                if (la.Kind == DTokens.CloseCurlyBrace)
                {
                    mbrace--;
                    if (mbrace <= 0) { break; }
                }
                lexer.NextToken();
            }
        }

        public string SkipToClosingParenthesis()
        {
            string ret = "";
            int mbrace = 0, round = 0;
            while (!EOF)
            {
                if (la.Kind == DTokens.OpenCurlyBrace) mbrace++;
                if (la.Kind == DTokens.CloseCurlyBrace) mbrace--;

                if (ThrowIfEOF(DTokens.CloseParenthesis)) break;
                if (la.Kind == DTokens.OpenParenthesis)
                {
                    round++;
                    lexer.NextToken(); continue;
                }
                if (la.Kind == DTokens.CloseParenthesis)
                {
                    round--;
                    if (mbrace < 1 && round < 1) { break; }
                }
                if (ret.Length < 2000) ret += strVal;
                lexer.NextToken();
            }
            return ret;
        }

        public string SkipToClosingSquares()
        {
            string ret = "";
            int mbrace = 0, round = 0;
            while (!EOF)
            {
                if (la.Kind == DTokens.OpenCurlyBrace) mbrace++;
                if (la.Kind == DTokens.CloseCurlyBrace) mbrace--;

                if (ThrowIfEOF(DTokens.CloseSquareBracket)) break;
                if (la.Kind == DTokens.OpenSquareBracket)
                {
                    round++;
                    lexer.NextToken(); continue;
                }
                if (la.Kind == DTokens.CloseSquareBracket)
                {
                    round--;
                    if (mbrace < 1 && round < 1) { break; }
                }
                if (ret.Length < 2000) ret += strVal;

                lexer.NextToken();
            }
            return ret;
        }

        public DLexer lexer;
        //public Errors errors;
        public DParser(DLexer lexer)
        {
            this.lexer = lexer;
            //errors = lexer.Errors;
            //errors.SynErr = new ErrorCodeProc(SynErr);
            lexer.OnComment += new AbstractLexer.CommentHandler(lexer_OnComment);
        }

        #region DDoc handling

        public DNode LastElement = null;
        string LastDescription = ""; // This is needed if some later comments are 'ditto'
        string CurrentDescription = "";
        bool HadEmptyCommentBefore = false;

        void lexer_OnComment(Comment comment)
        {
            if (comment.CommentType == Comment.Type.Documentation)
            {
                if (comment.CommentText != "ditto")
                {
                    HadEmptyCommentBefore= (CurrentDescription=="" && comment.CommentText == "");
                    CurrentDescription += (CurrentDescription == "" ? "" : "\r\n") + comment.CommentText;
                }
                else
                    CurrentDescription = LastDescription;

                /*
                 * /// start description
                 * void foo() /// description for foo()
                 * {}
                 */
                if (LastElement != null && LastElement.StartLocation.Line== comment.StartPosition.Line && comment.StartPosition.Column > LastElement.StartLocation.Column)
                {
                    LastElement.desc += (LastElement.desc == "" ? "" : "\r\n") + CurrentDescription;
                    LastDescription = CurrentDescription;
                    CurrentDescription = "";
                }
            }
        }

        #endregion

        StringBuilder qualidentBuilder = new StringBuilder();

        DToken t
        {
            [System.Diagnostics.DebuggerStepThrough]
            get
            {
                return (DToken)lexer.CurrentToken;
            }
        }

        /// <summary>
        /// lookAhead token
        /// </summary>
        DToken la
        {
            [System.Diagnostics.DebuggerStepThrough]
            get
            {
                return (DToken)lexer.LookAhead;
            }
        }

        public string CheckForDocComments()
        {
            string ret = CurrentDescription;
            if (CurrentDescription != "" || HadEmptyCommentBefore)
                LastDescription = CurrentDescription;
            CurrentDescription = "";
            return ret;
        }

        /// <summary>
        /// Check if current lookAhead DToken equals to n and skip that token.
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        protected bool Expect(int n, string reason)
        {
            if (la.Kind == n)
            {
                lexer.NextToken();
                return true;
            }
            else
            {
                SynErr(n, reason);
                return false;
            }
        }

        /// <summary>
        /// Retrieve string value of current lookAhead token
        /// </summary>
        protected string strVal
        {
            get
            {
                if (la.Kind == DTokens.Identifier || la.Kind == DTokens.Literal)
                    return la.Value;
                return DTokens.GetTokenString(la.Kind);
            }
        }

        protected bool ThrowIfEOF(int n)
        {
            if (la.Kind == DTokens.EOF)
            {
                SynErr(n, "End of file reached!");
                return true;
            }
            return false;
        }

        protected bool PeekMustBe(int n, string reason)
        {
            if (Peek(1).Kind == n)
            {
                lexer.NextToken();
            }
            else
            {
                SynErr(n, reason);
                return false;
            }
            return true;
        }

        /* Return the n-th token after the current lookahead token */
        void StartPeek()
        {
            lexer.StartPeek();
        }

        DToken Peek()
        {
            return lexer.Peek();
        }

        DToken Peek(int n)
        {
            lexer.StartPeek();
            DToken x = la;
            while (n > 0)
            {
                x = lexer.Peek();
                n--;
            }
            return x;
        }

        /* True, if ident is followed by ",", "=", "[" or ";"*/
        bool IsVarDecl()
        {
            int peek = Peek(1).Kind;
            return la.Kind == DTokens.Identifier &&
                (peek == DTokens.Comma || peek == DTokens.Assign || peek == DTokens.Semicolon);
        }

        bool EOF
        {
            get { return la == null || la.Kind == DTokens.EOF; }
        }



        /// <summary>
        /// Initializes and proceed parse procedure
        /// </summary>
        /// <param name="imports">List of imports in the module</param>
        /// <param name="fl">TODO: Folding marks</param>
        /// <returns>Completely parsed module structure</returns>
        public DNode Parse(string moduleName, out List<string> imports)
        {
            import = new List<string>();
            imports = import;

            BlockModifiers = new List<int>();
            BlockModifiers.Add(DTokens.Public);
            ExpressionModifiers = new List<int>();

            doc = new DNode(FieldType.Root);
            doc.name = moduleName;
            doc.startLoc = CodeLocation.Empty;
            doc.module = moduleName;
            ParseBlock(ref doc, false);

            doc.endLoc = GetCodeLocation(la);
            return doc;
        }

        /// <summary>
        /// Parses complete block from current lookahead DToken "{" until the last "}" on the same depth
        /// </summary>
        /// <param name="ret">Parent node</param>
        void ParseBlock(ref DNode ret, bool isFunctionBody)
        {
            int curbrace = 0;
            if (String.IsNullOrEmpty(ret.desc)) ret.desc = CheckForDocComments();
            List<int> prevBlockModifiers = new List<int>(BlockModifiers);
            ExpressionModifiers.Clear();
            BlockModifiers.Clear();
            BlockModifiers.Add(DTokens.Public);

            //Debug.Print("ParseBlock started ("+ret.name+")");

            if (la != null) ret.startLoc = ret.BlockStartLocation = GetCodeLocation(la);

            while (la == null || la.Kind != DTokens.EOF)
            {
                lexer.NextToken();
            blockcont:
                if (la.Kind == DTokens.EOF) { if (curbrace > 1) SynErr(DTokens.CloseCurlyBrace); break; }
                BlockModifiers = prevBlockModifiers;

                if (la.Kind == DTokens.Scope)
                {
                    if (Peek(1).Kind == DTokens.OpenParenthesis)
                    {
                        SkipToClosingParenthesis();
                        continue;
                    }
                }

                #region Modifiers
                if (DTokens.Modifiers[la.Kind])
                {
                    DToken pt = Peek(1);
                    int mod = la.Kind;

                    if (pt.Kind == DTokens.OpenParenthesis) // const>(<char)[]
                    {
                        if (Peek(2).Kind == DTokens.CloseParenthesis) // invariant() {...} - whatever this shall mean...something like that is possible in D!
                        {
                            lexer.NextToken(); // Skip modifier ID
                            lexer.NextToken(); // Skip "("
                            // assert(la.Kind==DTokens.CloseParenthesis)
                            if (Peek(1).Kind == DTokens.OpenCurlyBrace)
                            {
                                SkipToClosingBrace();
                            }

                            continue;
                        }
                    }
                    else if (pt.Kind == DTokens.Colon)// private>:<
                    {
                        if (!BlockModifiers.Contains(mod))
                        {
                            if (DTokens.VisModifiers[mod]) DTokens.RemoveVisMod(BlockModifiers);
                            BlockModifiers.Add(mod);
                        }
                        continue;
                    }
                    else if (pt.Kind == DTokens.OpenCurlyBrace) // public >{<...}
                    {
                        lexer.NextToken(); // Skip modifier
                        DNode tblock = new DNode(ret.fieldtype);
                        ParseBlock(ref tblock, isFunctionBody);

                        foreach (DNode dt in tblock) // Apply modifier to parsed children
                        {
                            if (!dt.modifiers.Contains(mod)) // static package int a;
                            {
                                if (DTokens.VisModifiers[mod]) DTokens.RemoveVisMod(dt.modifiers);
                                dt.modifiers.Add(mod);
                            }
                        }

                        ret.Children.AddRange(tblock.Children);
                        continue;
                    }
                    else
                    {
                        DToken pt2 = pt;
                        pt = lexer.Peek();
                        bool hasFollowingMods = false;
                        while (pt.Kind != DTokens.EOF)
                        {
                            if (DTokens.Modifiers[pt.Kind]) // static >const<
                            {
                                pt = lexer.Peek();
                                if (pt.Kind == DTokens.OpenCurlyBrace) // static const >{<...}
                                {
                                    hasFollowingMods = true;
                                    break;
                                }
                            }
                            else
                                break;
                            pt = lexer.Peek();
                        }

                        if (!hasFollowingMods && la.Kind == DTokens.Const && pt2.Kind == DTokens.Identifier && pt.Kind == DTokens.Assign) // const >MyCnst2< = 2; // similar to enum MyCnst = 1;
                        {
                            DNode cdt = ParseEnum();
                            cdt.Type = new DTokenDeclaration(DTokens.Int);
                            cdt.modifiers.Add(DTokens.Const);
                            cdt.TypeToken = DTokens.Int;
                            cdt.Parent = ret;
                            ret.Children.Add(cdt);
                        }

                        if (!ExpressionModifiers.Contains(mod) && !hasFollowingMods) // static package int a;
                        {
                            if (DTokens.VisModifiers[mod]) DTokens.RemoveVisMod(ExpressionModifiers);
                            ExpressionModifiers.Add(mod);
                        }
                        continue;
                    }

                }
                #endregion

                #region Normal Expressions
                if (DTokens.BasicTypes[la.Kind] || la.Kind == DTokens.Identifier ||
                    la.Kind == DTokens.Typeof || DTokens.Modifiers[la.Kind])
                {
                    bool isTypeOf = la.Kind == DTokens.Typeof;

                    #region Within Function Body
                    if (isFunctionBody && !isTypeOf)
                    {
                        DToken pk = Peek(1);
                        switch (pk.Kind)
                        {
                            case DTokens.Dot: // Package.foo();
                                continue;

                            case DTokens.Not: // Type!(int,b)();
                                int par = 0;
                                bool isCall = false;
                                Peek(); // skip peeked '!'
                                while ((pk = Peek()).Kind != DTokens.EOF)
                                {
                                    if (pk.Kind == DTokens.OpenParenthesis)
                                    {
                                        if (par < 0) // Template!( |Here we start; par=0|...(.|par=1|.). |par=0|.. ) |par=-1| >(<
                                        {
                                            isCall = true; break;
                                        }
                                        par++;
                                    }
                                    if (pk.Kind == DTokens.CloseParenthesis) par--;

                                    if (pk.Kind == DTokens.Semicolon || pk.Kind == DTokens.OpenCurlyBrace) { isCall = false; break; }
                                }
                                if (!isCall) break;
                                SkipToSemicolon();
                                continue;

                            case DTokens.Colon: // part:
                                lexer.NextToken();
                                continue;

                            case DTokens.OpenSquareBracket: // array[0]+=5; char[] buf;
                                #region Check if Var Decl is done
                                int mbrace2 = 0;
                                bool isDecl = false;
                                while ((pk = Peek()).Kind != DTokens.EOF)
                                {
                                    switch (pk.Kind)
                                    {
                                        case DTokens.OpenSquareBracket:
                                            mbrace2++;
                                            break;
                                        case DTokens.CloseSquareBracket:
                                            mbrace2--;
                                            break;
                                        case DTokens.Dot:
                                            if (mbrace2 > 0) continue;
                                            pk = Peek();
                                            if (pk.Kind == DTokens.Identifier) // array[i].foo(); array[i].x=2;
                                            {
                                                continue;
                                            }
                                            break;
                                    }

                                    if (mbrace2 < 1)
                                    {
                                        if (DTokens.AssignOps[pk.Kind])
                                        {
                                            isDecl = pk.Kind == DTokens.Assign;
                                            break;
                                        }

                                        if (pk.Kind == DTokens.Identifier)
                                        {
                                            pk = Peek();
                                            if (pk.Kind == DTokens.Comma || // string[] a,b;
                                                pk.Kind == DTokens.Assign || // string[] stringArray=...;
                                                pk.Kind == DTokens.Semicolon || // string[] stringArray;
                                                pk.Kind == DTokens.OpenSquareBracket) // char[] ID=value;
                                            { isDecl = true; goto parseexpr; }
                                        }
                                    }
                                }
                                #endregion
                                if (isDecl) goto default;
                            sqbracket:
                                if (la.Kind == DTokens.OpenSquareBracket)
                                    SkipToClosingSquares();
                                if (la.Kind == DTokens.CloseSquareBracket)
                                {
                                    pk = Peek(1);
                                    if (pk.Kind == DTokens.OpenSquareBracket) // matrix[0][4]=1;
                                    {
                                        lexer.NextToken(); // Skip last "]"
                                        goto sqbracket;
                                    }
                                }
                                goto default;

                            case DTokens.OpenParenthesis: // becomes handled few lines below!
                                break;

                            default:
                                if (pk.Kind == DTokens.Increment ||  // a++;
                                    pk.Kind == DTokens.Decrement)
                                {
                                    lexer.NextToken();
                                    continue;
                                }
                                if (DTokens.AssignOps[pk.Kind])// b<<=4;
                                {
                                    SkipToSemicolon();//ParseAssignIdent(ref ret, true);
                                    continue;
                                }
                                if (pk.Kind == DTokens.Semicolon) continue;

                                if (DTokens.Conditions[pk.Kind]) // p!= null || p<1
                                {
                                    SkipToSemicolon();
                                    continue;
                                }
                                break;
                        }
                    }
                    #endregion
                parseexpr:
                    if (DTokens.ClassLike[Peek(1).Kind]) break;


                    /*
                     * Could be function call (foo>(<))
                     * but it may be function pointer declaration (int >(<*foo)();)
                     * don't confuse it with a function call that contains a * as one of the first arguments like foo(*pointer);
                     */
                    if (Peek(1).Kind == DTokens.OpenParenthesis)
                    {
                        DToken pk = Peek();
                        if (pk.Kind != DTokens.Times)
                        {
                            SkipToSemicolon();
                            continue;
                        }
                        else
                        {
                            #region Search for a possible function pointer definition
                            int par = 0;
                            bool IsFunctionDefinition = false;
                            while ((pk = Peek()).Kind != DTokens.EOF)
                            {
                                if (pk.Kind == DTokens.OpenParenthesis)
                                {
                                    if (par < 0)
                                    {
                                        IsFunctionDefinition = true;
                                        break;
                                    }
                                    par++;
                                }
                                if (pk.Kind == DTokens.CloseParenthesis) par--;

                                if (pk.Kind == DTokens.Semicolon || pk.Kind == DTokens.CloseCurlyBrace || pk.Kind == DTokens.OpenCurlyBrace) break;
                            }
                            if (!IsFunctionDefinition)
                            {
                                SkipToSemicolon();
                                continue;
                            }
                            #endregion
                        }
                    }

                    #region Modifier assessment
                    bool cvm = DTokens.ContainsVisMod(ExpressionModifiers);
                    foreach (int m in BlockModifiers)
                    {
                        if (!ExpressionModifiers.Contains(m))
                        {
                            if (cvm && DTokens.VisModifiers[m]) continue;
                            ExpressionModifiers.Add(m);
                        }
                    }
                    List<int> TExprMods = new List<int>(ExpressionModifiers);
                    ExpressionModifiers.Clear();

                    #endregion

                    DNode tv = ParseExpression();
                    if (tv != null)
                    {
                        LastElement = tv;
                        tv.modifiers.AddRange(TExprMods);
                        tv.module = ret.module;
                        tv.Parent = ret;
                        ret.Add(tv);

                        if (la.Kind == DTokens.Comma) goto blockcont;
                    }
                    continue;
                }
                #endregion

                #region Special and other Tokens
                switch (la.Kind)
                {
                    case DTokens.Times: // *ptr=123;
                        if (!isFunctionBody)
                            SynErr(la.Kind, "'*' not allowed here; Identifier expected");
                        SkipToSemicolon();
                        break;

                    case DTokens.OpenParenthesis: // (...);
                        if (!isFunctionBody)
                            SynErr(la.Kind, "C-style cast not allowed here");
                        SkipToSemicolon();
                        break;

                    #region Custom Allocators
                    case DTokens.Delete:
                    case DTokens.New:
                        bool IsAlloc = la.Kind == DTokens.New;
                        if (isFunctionBody) break;

                        // This is for handling custom allocators (new(uint size){...})
                        if (!isFunctionBody)
                        {
                            if (!PeekMustBe(DTokens.OpenParenthesis, "Expected \"(\" for declaring a custom (de-)allocator!"))
                            {
                                SkipToClosingBrace();
                                break;
                            }
                            DMethod custAlloc = new DMethod();
                            if (IsAlloc)
                            {
                                custAlloc.name = "new";
                                custAlloc.Type = new PointerDecl(new DTokenDeclaration(DTokens.Void));
                            }
                            else
                            {
                                custAlloc.name = "delete";
                                custAlloc.Type = new DTokenDeclaration(DTokens.Void);
                            }
                            custAlloc.TypeToken = DTokens.New;
                            lexer.NextToken();
                            ParseFunctionArguments(ref custAlloc);
                            if (!Expect(DTokens.CloseParenthesis, "Expected \")\" for declaring a custom (de-)allocator!"))
                            {
                                SkipToClosingBrace();
                                break;
                            }
                            custAlloc.modifiers.Add(DTokens.Private);
                            DNode _custAlloc = custAlloc;
                            ParseBlock(ref _custAlloc, true);

                            custAlloc.module = ret.module;
                            custAlloc.Parent = ret;
                            ret.Add(custAlloc);
                        }
                        break;
                    #endregion
                    case DTokens.Cast:
                        SynErr(la.Kind, "Cast cannot be done at front of a statement");
                        SkipToSemicolon();
                        break;
                    case DTokens.With:
                        if (PeekMustBe(DTokens.OpenParenthesis, "Error parsing \"with()\" Expression: \"(\" expected!"))
                        {
                            SkipToClosingParenthesis();
                            if (la.Kind != DTokens.CloseParenthesis) SynErr(DTokens.CloseParenthesis, "Error parsing \"with()\" Expression: \")\" expected!");
                        }
                        break;
                    case DTokens.Asm: // Inline Assembler
                        SkipToClosingBrace();
                        break;
                    case DTokens.Case:
                        while (!EOF)
                        {
                            lexer.NextToken();
                            if (la.Kind == DTokens.Colon) break;
                        }
                        break;
                    case DTokens.Catch:
                        if (Peek(1).Kind == DTokens.OpenParenthesis)
                            SkipToClosingParenthesis();
                        break;
                    case DTokens.Debug:
                        if (Peek(1).Kind == DTokens.OpenParenthesis)
                            SkipToClosingParenthesis();
                        break;
                    case DTokens.Goto:
                        SkipToSemicolon();
                        break;
                    case DTokens.Throw:
                    case DTokens.Return:
                        SkipToSemicolon();
                        break;
                    case DTokens.Unittest:
                        if (Peek(1).Kind == DTokens.OpenCurlyBrace)
                        {
                            SkipToClosingBrace();
                        }
                        break;
                    case DTokens.Do: // do {...} while(...);
                        if (Peek(1).Kind == DTokens.OpenCurlyBrace)
                        {
                            SkipToClosingBrace();
                            Expect(DTokens.CloseCurlyBrace, "Error parsing do Expression: \"}\" after \"do\" block expected!");
                        }
                        else
                        {
                            SkipToSemicolon();
                            Expect(DTokens.Semicolon, "Semicolon after statement expected!");
                        }

                        if (Expect(DTokens.While, "while() expected!"))
                        {
                            SkipToSemicolon();
                        }
                        break;
                    case DTokens.For:
                    case DTokens.Foreach:
                    case DTokens.Foreach_Reverse:
                    case DTokens.Switch:
                    case DTokens.While:
                    case DTokens.If: // static if(...) {}else if(...) {} else{}
                        if (PeekMustBe(DTokens.OpenParenthesis, "'(' expected!"))
                        {
                            SkipToClosingParenthesis();
                            break;
                        }
                        break;
                    case DTokens.Else:
                        break;
                    case DTokens.Comma:
                        if (ret.Count < 1) break;
                        if (!PeekMustBe(DTokens.Identifier, "Expected variable identifier!"))
                        {
                            SkipToSemicolon();
                            break;
                        }
                        // MyType a,b,c,d;
                        DNode prevExpr = (DNode)ret.Children[ret.Count - 1];
                        if (prevExpr.fieldtype == FieldType.Variable)
                        {
                            DVariable tv = new DVariable();
                            if (tv == null) continue;
                            tv.modifiers = prevExpr.modifiers;
                            tv.startLoc = prevExpr.startLoc;
                            tv.Type = prevExpr.Type;
                            tv.TypeToken = prevExpr.TypeToken;
                            tv.name = strVal;
                            tv.endLoc = GetCodeLocation(la);
                            lexer.NextToken(); // Skip var id
                            if (la.Kind == DTokens.Assign) tv.Value = ParseAssignIdent(false);
                            else if (Peek(1).Kind == DTokens.Assign)
                            {
                                lexer.NextToken();
                                tv.Value = ParseAssignIdent(false);
                            }
                            LastElement = tv;
                            ret.Add(tv);

                            if (la.Kind == DTokens.Comma) goto blockcont; // Another declaration is directly following
                        }
                        break;
                    case DTokens.EOF:
                        if (t.Kind != DTokens.CloseCurlyBrace) SynErr(DTokens.CloseCurlyBrace);
                        ret.endLoc = GetCodeLocation(la);
                        BlockModifiers = prevBlockModifiers;
                        return;
                    case DTokens.Align:
                    case DTokens.Version:
                        lexer.NextToken(); // Skip "version"

                        if (la.Kind == DTokens.Assign) // version=xxx
                        {
                            SkipToSemicolon();
                            break;
                        }

                        Expect(DTokens.OpenParenthesis, "'(' expected!");
                        string version = strVal; // version(xxx)
                        if (version == "Posix" && Peek(1).Kind == DTokens.OpenCurlyBrace) SkipToClosingBrace();
                        break;
                    case DTokens.Extern:
                        if (Peek(1).Kind == DTokens.OpenParenthesis)
                            SkipToClosingParenthesis();
                        break;
                    case DTokens.CloseCurlyBrace: // }
                        curbrace--;
                        if (curbrace < 0)
                        {
                            ret.endLoc = GetCodeLocation(la);
                            BlockModifiers = prevBlockModifiers;
                            ExpressionModifiers.Clear();

                            CurrentDescription = "";
                            return;
                        }
                        break;
                    case DTokens.OpenCurlyBrace: // {
                        curbrace++;
                        break;
                    case DTokens.Enum:
                        DNode mye = ParseEnum();
                        if (mye != null)
                        {
                            mye.Parent = ret;
                            mye.module = ret.module;

                            if (mye.name != "")
                            {
                                LastElement = mye;
                                ret.Add(mye);
                            }
                            else
                            {
                                foreach (DNode ch in mye)
                                {
                                    ch.Parent = ret;
                                    ch.module = ret.module;
                                }
                                ret.Children.AddRange(mye.Children);
                            }
                        }
                        break;
                    case DTokens.Super:
                        if (isFunctionBody) // Every "super" in a function body can only be a call....
                        {
                            SkipToSemicolon();
                            break;
                        }
                        else SynErr(DTokens.Super);
                        break;
                    case DTokens.This:
                        if (isFunctionBody) // Every "this" in a function body can only be a call....
                        {
                            SkipToSemicolon();
                            break;
                        }

                        #region Modifier assessment
                        bool cvm = DTokens.ContainsVisMod(ExpressionModifiers);
                        foreach (int m in BlockModifiers)
                        {
                            if (!ExpressionModifiers.Contains(m))
                            {
                                if (cvm && DTokens.VisModifiers[m]) continue;
                                ExpressionModifiers.Add(m);
                            }
                        }
                        List<int> TExprMods = new List<int>(ExpressionModifiers);
                        ExpressionModifiers.Clear();

                        #endregion

                        string cname = "";
                        if (t.Kind == DTokens.Tilde) // "~"
                            cname = "~" + ret.name;
                        else
                            cname = ret.name;

                        DNode ctor = ParseExpression();
                        if (ctor != null)
                        {
                            LastElement = ctor;
                            if (ret.fieldtype == FieldType.Root && !TExprMods.Contains(DTokens.Static))
                            {
                                SemErr(DTokens.This, ctor.startLoc.Column, ctor.startLoc.Line, "Module Constructors must be static!");
                            }

                            ctor.modifiers.AddRange(TExprMods);
                            ctor.name = cname;
                            ctor.fieldtype = FieldType.Constructor;
                            ctor.endLoc = GetCodeLocation(la);

                            ctor.Parent = ret;
                            ctor.module = ret.module;

                            ret.Add(ctor);
                        }
                        break;
                    case DTokens.Class:
                    case DTokens.Template:
                    case DTokens.Struct:
                    case DTokens.Union:
                    case DTokens.Interface:

                        if (Peek(1).Kind == DTokens.OpenCurlyBrace) // struct {...}
                        {
                            break;
                        }

                        DNode myc = ParseClass();
                        if (myc != null)
                        {
                            LastElement = myc;
                            myc.module = ret.module;
                            myc.Parent = ret;
                            ret.Add(myc);
                        }
                        continue;
                    case DTokens.Module:
                        lexer.NextToken();
                        ret.module = SkipToSemicolon();
                        break;
                    case DTokens.Typedef:
                    case DTokens.Alias:
                        // typedef void* function(int a) foo;
                        lexer.NextToken(); // Skip alias|typedef
                        DNode aliasType = ParseExpression();
                        if (aliasType == null) break;
                        aliasType.fieldtype = FieldType.AliasDecl;
                        LastElement = aliasType;
                        ret.Add(aliasType);

                        while (la.Kind == DTokens.Comma)
                        {
                            if (!PeekMustBe(DTokens.Identifier, "Identifier expected")) break;
                            DNode other = new DNode();
                            other.fieldtype = FieldType.AliasDecl;
                            other.Assign(aliasType);
                            other.name = strVal;
                            LastElement = other;
                            ret.Add(other);
                            lexer.NextToken();
                        }

                        break;
                    case DTokens.Import:
                        ParseImport();
                        continue;
                    case DTokens.Mixin:
                    case DTokens.Assert:
                    case DTokens.Pragma:
                        SkipToSemicolon();
                        break;
                    default:
                        break;
                }
                #endregion
            }
            
            // Debug.Print("ParseBlock ended (" + ret.name + ")");
        }

        /// <summary>
        /// import abc.def, efg.hij, xyz;
        /// </summary>
        void ParseImport()
        {
            if (la.Kind != DTokens.Import)
                SynErr(DTokens.Import);
            string ts = "";

            List<string> tl = new List<string>();
            while (!EOF && la.Kind != DTokens.Semicolon)
            {
                lexer.NextToken();
                if (ThrowIfEOF(DTokens.Semicolon)) return;
                switch (la.Kind)
                {
                    default:
                        ts += strVal;
                        break;
                    case DTokens.Comma:
                    case DTokens.Semicolon:
                        tl.Add(ts);
                        ts = "";
                        break;
                    case DTokens.Colon:
                    case DTokens.Assign:
                        ts = "";
                        break;
                }
            }

            if (la.Kind == DTokens.Semicolon) import.AddRange(tl);
        }


        void ParseTemplateArguments(ref DNode v)
        {
            int psb = 0;// ()
            DVariable targ = null;
            string targtype = "";

            if (la.Kind == DTokens.OpenParenthesis) psb = -1;
            while (la.Kind != DTokens.EOF)
            {
                if (ThrowIfEOF(DTokens.CloseParenthesis))
                    return;

                switch (la.Kind)
                {
                    case DTokens.OpenParenthesis:
                        psb++;
                        if (targ != null) targtype += "(";
                        break;
                    case DTokens.CloseParenthesis:
                        psb--;
                        if (psb < 0)
                        {
                            if (targ != null)
                            {
                                targ.endLoc = ToCodeEndLocation(t);
                                targ.Type = new NormalDeclaration(targtype);
                                targ.name = targtype;
                                v.TemplateParameters.Add(targ);
                            }
                            return;
                        }
                        if (targ != null) targtype += ")";
                        break;
                    case DTokens.Comma:
                        if (psb > 1) break;
                        if (targ == null) { SkipToClosingBrace(); break; }
                        targ.endLoc = GetCodeLocation(la);
                        targ.Type = new NormalDeclaration(targtype);
                        targ.name = targtype;
                        v.TemplateParameters.Add(targ);
                        targ = null;
                        break;
                    case DTokens.Dot:
                        if (Peek(1).Kind == DTokens.Dot && Peek(2).Kind == DTokens.Dot) // "..."
                        {
                            if (targ == null) { targ = new DVariable(); targtype = ""; }

                            if (targ.name == "") targ.name = "...";
                            if (targtype != "") targ.name = targtype;

                            targ.StartLocation = la.Location;
                            targ.EndLocation = la.EndLocation;
                            targ.endLoc.Column += 3; // three dots (...)
                            targ.Type = targtype != "" ? new VarArgDecl(new NormalDeclaration(targtype)) : new VarArgDecl();
                            v.TemplateParameters.Add(targ);
                            targ = null;
                        }
                        break;
                    case DTokens.Alias:
                        if (targ == null) { targ = new DVariable(); targtype = ""; }
                        targ.startLoc = GetCodeLocation(la);
                        targ.modifiers.Add(la.Kind);
                        break;
                    default:
                        if (targ == null) { targtype = ""; targ = new DVariable(); targ.startLoc = ToCodeEndLocation(la); }

                        if (DTokens.Modifiers[la.Kind] && Peek(1).Kind != DTokens.OpenParenthesis) // const int a
                        {
                            targ.modifiers.Add(la.Kind);
                            break;
                        }

                        targtype += strVal;
                        break;
                }
                lexer.NextToken();
            }
        }

        /// <summary>
        /// Parses all variable declarations when "(" is the lookahead DToken and retrieves them into v.param. 
        /// Thereafter ")" will be lookahead
        /// </summary>
        /// <param name="v"></param>
        void ParseFunctionArguments(ref DMethod v)
        {
            int psb = 0;// ()
            DNode targ = null;
            // int[]* MyFunction(in string[]* arg, uint function(aa[]) funcarg, ref MyType b)
            while (la.Kind != DTokens.EOF)
            {
                if (ThrowIfEOF(DTokens.CloseParenthesis))
                    return;

                switch (la.Kind)
                {
                    case DTokens.OpenParenthesis:
                        psb++;
                        break;
                    case DTokens.CloseParenthesis:
                        psb--;
                        if (psb < 1)
                        {
                            if (targ != null)
                            {
                                targ.endLoc = ToCodeEndLocation(t);
                                v.Parameters.Add(targ);
                            }
                            return;
                        }
                        break;
                    case DTokens.Comma:
                        if (targ == null) { SkipToClosingBrace(); break; }
                        targ.endLoc = GetCodeLocation(la);
                        v.Parameters.Add(targ);
                        targ = null;
                        break;
                    case DTokens.Dot:
                        if (Peek(1).Kind == DTokens.Dot && Peek(2).Kind == DTokens.Dot) // "..."
                        {
                            if (targ == null) targ = new DVariable();

                            targ.Type = new VarArgDecl();
                            targ.name = "...";

                            targ.startLoc = GetCodeLocation(la);
                            targ.endLoc = GetCodeLocation(la);
                            targ.endLoc.Column += 3; // three dots (...)

                            v.Parameters.Add(targ);
                            targ = null;
                        }
                        break;
                    case DTokens.Alias:
                        if (targ == null) targ = new DVariable();
                        targ.modifiers.Add(la.Kind);
                        break;
                    default:
                        if (DTokens.Modifiers[la.Kind] && Peek(1).Kind != DTokens.OpenParenthesis) // const int a
                        {
                            if (targ == null) targ = new DVariable();
                            targ.modifiers.Add(la.Kind);
                            break;
                        }
                        if (DTokens.BasicTypes[la.Kind] || la.Kind == DTokens.Identifier || la.Kind == DTokens.Typeof)
                        {
                            if (targ == null) targ = new DVariable();
                            if (Peek(1).Kind == DTokens.Dot) break;

                            targ.startLoc = GetCodeLocation(la);
                            targ.TypeToken = la.Kind;

                            targ.Type = ParseTypeIdent(out targ.name);

                            if (la.Kind == DTokens.Comma || (la.Kind == DTokens.CloseParenthesis && Peek(1).Kind == DTokens.Semicolon))// size_t wcslen(in wchar *>);<
                            {
                                continue;
                            }
                            /*
                            if (la.Kind == DTokens.Colon) // void foo(T>:<Object[],S[],U,V) {...}
                            {
                                lexer.NextToken(); // Skip :
                                targ.Type = new InheritanceDecl(targ.Type);
                                (targ.Type as InheritanceDecl).InheritedClass = ParseTypeIdent();
                                DToken pk2 = Peek(1);
                            }

                            if (la.Kind == DTokens.Identifier) targ.name = strVal;*/

                            if (Peek(1).Kind == DTokens.Assign) // Argument has default argument
                            {
                                if (targ is DVariable)
                                    (targ as DVariable).Value = ParseAssignIdent(true);
                                else
                                    ParseAssignIdent(true);

                                if (la.Kind == DTokens.CloseParenthesis && (Peek(1).Kind == DTokens.Comma || Peek(1).Kind == DTokens.CloseParenthesis)) lexer.NextToken(); // void foo(int a=bar(>),<bool b)
                                continue;
                            }
                        }
                        break;
                }
                lexer.NextToken();
            }
        }

        /// <summary><![CDATA[
        /// enum{
        /// a>=1<,
        /// b=2,
        /// c
        /// }
        /// =1;
        /// =-1234;
        /// =&a;
        /// =*b;
        /// =cast(uint) -1
        /// =cast(char*) "",
        /// =*(cast(int[]*)b);
        /// =delegate void(int i) {...};
        /// =MyType.ConstVal
        /// =1+5;
        /// =(EnumVal1 + EnumVal2);
        /// =EnumVal1 | EnumVal2;
        /// ]]></summary>
        /// <returns></returns>
        string ParseAssignIdent(bool isFuncParam)
        {
            string ret = "";
            while (!ThrowIfEOF(DTokens.Semicolon))
            {
                lexer.NextToken();

                if (la.Kind == DTokens.Comma || la.Kind == DTokens.Semicolon || la.Kind == DTokens.CloseParenthesis || la.Kind == DTokens.CloseCurlyBrace)
                    break;

                if (la.Kind == DTokens.OpenCurlyBrace) { SkipToClosingBrace(); }
                if (la.Kind == DTokens.OpenParenthesis) { ret += "(" + SkipToClosingParenthesis(); }

                ret += (la.Kind == DTokens.Identifier || la.Kind == DTokens.Delegate || la.Kind == DTokens.Function ? " " : "") + strVal;
            }
            return ret.Trim();
        }

        /// <summary>
        /// MyType
        /// uint[]*
        /// const(char)*
        /// invariant(char)[]
        /// int[] function(char[int[]], int function() mf, ref string y)[]
        /// immutable(char)[] 
        /// int ABC;
        /// int* ABC;
        /// int[] ABC;
        /// int[]* ABC;
        /// myclass!(...) ABC;
        /// myclass.staticType ABC;
        /// int[]* delegate(...)[] ABC;
        /// </summary>
        /// <param name="identifierOnly"></param>
        /// <param name="hasClampMod"></param>
        /// <returns></returns>
        TypeDeclaration ParseTypeIdent(out string VariableName)
        {
            VariableName = null;
            Stack<TypeDeclaration> declStack = new Stack<TypeDeclaration>();
            bool IsInit = true;
            bool IsBaseTypeAnalysis = ((t != null && t.Kind == DTokens.Colon) || la.Kind == DTokens.Colon); // class ABC>:<Object {}
            if (la.Kind == DTokens.Colon) lexer.NextToken(); // Skip ':'
            bool IsCStyleDeclaration = false; // char abc>[<30];

            DToken pk = null;
            bool IsInMemberModBracket = false;
            while (la.Kind != DTokens.EOF)
            {
                pk = Peek(1);

                if (IsInit && DTokens.ParamModifiers[la.Kind]) // ref int
                {
                    declStack.Push(new DTokenDeclaration(la.Kind));
                    lexer.NextToken(); // Skip ref, inout, out ,in
                    continue;
                }

                if ((la.Kind == DTokens.Const || la.Kind == DTokens.Immutable || la.Kind == DTokens.Shared) && pk.Kind == DTokens.OpenParenthesis) // const(...)
                {
                    declStack.Push(new MemberFunctionAttributeDecl(la.Kind));
                    IsInMemberModBracket = true;
                    lexer.NextToken(); // Skip const
                    lexer.NextToken(); // Skip (
                    IsInit = false;
                    continue;
                }

                if (IsInit && la.Kind == DTokens.Auto && pk.Kind == DTokens.Ref) // auto ref foo()...
                {
                    lexer.NextToken(); // Skip 'auto'
                    declStack.Push(new NormalDeclaration("auto ref"));
                    IsInit = false;
                    continue;
                }

                if (la.Kind == DTokens.Literal) // int[>3<];
                {
                    if (t.Kind != DTokens.OpenSquareBracket)
                    {
                        SynErr(DTokens.OpenSquareBracket, "Literals only allowed in array declarations");
                        goto do_return;
                    }

                    declStack.Push(new NormalDeclaration(strVal));
                    lexer.NextToken(); // Skip literal
                    continue;
                }

                if (la.Kind == DTokens.Identifier) // int* >ABC<;
                {
                    if (pk.Kind == DTokens.OpenSquareBracket) // int ABC>[<1234];
                    {
                        VariableName = strVal;
                        IsCStyleDeclaration = true;
                    }
                    else if (pk.Kind == DTokens.OpenParenthesis ||  // void foo>(<...) {...}
                            pk.Kind == DTokens.Comma ||             // void foo(bool a>,<int b)
                            pk.Kind == DTokens.CloseParenthesis ||  // void foo(bool a,int b>)< {}
                            pk.Kind == DTokens.Semicolon ||         // int abc>;<
                            pk.Kind == DTokens.Colon ||             // class ABC>:<Object {...}
                            pk.Kind == DTokens.OpenCurlyBrace ||    // class ABC:Object>{<...}
                            pk.Kind == DTokens.Assign               // int[] foo>=<...;
                       )
                    {
                        if (!IsInit)
                            VariableName = strVal;
                        else declStack.Push(new NormalDeclaration(strVal));
                        goto do_return;
                    }
                }

                /*
                 *  This will happen only if a identifier is needed. 
                 */
                if (IsInit /* int* */ ||
                    ((IsInMemberModBracket /* const(ABC...) */ || (t != null && t.Kind == DTokens.Not)) || /* List!(ABC...) */
                    (t != null && t.Kind == DTokens.OpenParenthesis) /* Template!>(>abc) */ ||
                    (t != null && t.Kind == DTokens.Dot) || // >.<MyIdent
                    (t != null && t.Kind == DTokens.OpenSquareBracket && la.Kind != DTokens.CloseSquareBracket) /* int[><] */))
                {
                    if (!DTokens.BasicTypes[la.Kind] && la.Kind != DTokens.Identifier)
                    {
                        SynErr(la.Kind, "Expected identifier or base type!");
                        goto do_return;
                    }
                    //lexer.NextToken(); // Skip token that is in front of the identifier

                    if (DTokens.BasicTypes[la.Kind])
                        declStack.Push(new DTokenDeclaration(la.Kind));
                    else declStack.Push(new NormalDeclaration(strVal));
                }


                switch (la.Kind)
                {
                    case DTokens.Delegate: // myType*[] >delegate<(...) asdf;
                    case DTokens.Function:
                        DelegateDeclaration dd = new DelegateDeclaration();
                        if (declStack.Count < 1)// int a; >delegate<(...) asdf;
                        {
                            SynErr(la.Kind, "Declaration expected, not '" + strVal + "'!");
                            goto do_return;
                        }
                        dd.ReturnType = declStack.Pop();
                        declStack.Push(dd);

                        if (!PeekMustBe(DTokens.OpenParenthesis, "Expected '('!"))
                            goto do_return;

                        lexer.NextToken(); // Skip '('
                        #region Parse delegate parameters
                        if (la.Kind == DTokens.CloseParenthesis) break;//  void delegate(>)< asdf;
                        while (!ThrowIfEOF(DTokens.CloseParenthesis))
                        {
                            DVariable dv = new DVariable();
                            dv.Type = ParseTypeIdent(out dv.name);

                            lexer.NextToken(); // Skip last token parsed, can theoretically only be an identifier

                            // Do not expect a parameter id here!

                            if (la.Kind == DTokens.Assign) // void delegate(int i>=<5, bool a=false)
                                dv.Value = ParseAssignIdent(true);

                            dd.Parameters.Add(dv);

                            if (la.Kind == DTokens.CloseParenthesis) break;
                            if (la.Kind == DTokens.Comma) lexer.NextToken();
                        }
                        #endregion
                        break;

                    case DTokens.OpenParenthesis: // void >(<*foo)();
                        if (pk.Kind != DTokens.Times)
                            goto do_return;

                        lexer.NextToken(); // Skip '('
                        //TODO: possible but rare array declaration | void (*>[<]foo)();
                        if (!PeekMustBe(DTokens.Identifier, "Identifier expected!"))
                            goto do_return;

                        VariableName = strVal; // void (*>foo<)();
                        lexer.NextToken(); // Skip id
                        goto do_return;

                    case DTokens.Assign:
                    case DTokens.Colon:
                    case DTokens.Semicolon: // int;
                        if (!IsCStyleDeclaration)
                            SynErr(la.Kind, "Expected an identifier!");
                        goto do_return;

                    case DTokens.Comma:// void foo(T>,< U)()
                        goto do_return;
                    case DTokens.OpenCurlyBrace: // enum abc >{< ... }
                        if (t.Kind != DTokens.Identifier && !DTokens.BasicTypes[t.Kind] && !IsBaseTypeAnalysis)
                        {
                            SynErr(la.Kind, "Found '{' when expecting ')'!");
                            SkipToClosingBrace();
                        }
                        goto do_return;
                    case DTokens.CloseCurlyBrace: // int asdf; aaa>}<
                        if (t.Kind == DTokens.Identifier)
                            SynErr(la.Kind, "Found '}' when expecting ';'!");
                        else // int aaa}
                            SynErr(la.Kind, "Found '}' when expecting identifier!");
                        goto do_return;

                    case DTokens.CloseParenthesis: // const(...>)< | Template!(...>)< | void foo(T,U>)<()
                        if (declStack.Count < 2) // class myc(Type1>)<
                            goto do_return;

                        if (IsInMemberModBracket)
                            IsInMemberModBracket = false;

                        TypeDeclaration innerType = declStack.Pop();
                        if (declStack.Count > 0 && declStack.Peek() is MemberFunctionAttributeDecl)
                        {
                            MemberFunctionAttributeDecl attrDecl = declStack.Pop() as MemberFunctionAttributeDecl;
                            attrDecl.Base = innerType;
                            declStack.Push(attrDecl);
                        }
                        else if (declStack.Count > 0 && declStack.Peek() is TemplateDecl)
                        {
                            TemplateDecl templDecl = declStack.Pop() as TemplateDecl;
                            templDecl.Template = innerType;
                            declStack.Push(templDecl);
                        }
                        else
                        {
                            SynErr(DTokens.CloseParenthesis, "Type declaration parsing error! Perhaps there are too much closing parentheses (')')");
                            return declStack.Pop();
                        }
                        break;

                    case DTokens.CloseSquareBracket:
                        TypeDeclaration keyType = new DTokenDeclaration(DTokens.Int); // default key type is int
                        if (t.Kind != DTokens.OpenSquareBracket) // int>[<] abc;
                        {
                            keyType = declStack.Pop();
                            if (declStack.Count < 1 || !(declStack.Peek() is ArrayDecl))
                            {
                                SynErr(DTokens.CloseParenthesis, "Type declaration parsing error! Perhaps there are too much closing parentheses (']')");
                                return null;
                            }
                        }
                        ArrayDecl arrDecl = declStack.Pop() as ArrayDecl;
                        arrDecl.KeyType = keyType;
                        declStack.Push(arrDecl);
                        break;

                    case DTokens.Times: // int>*<
                        declStack.Push(new PointerDecl(declStack.Pop()));
                        break;

                    case DTokens.Not: // Template>!<(...)
                        lexer.NextToken(); // Skip !

                        TemplateDecl templDecl_ = new TemplateDecl(declStack.Pop());
                        declStack.Push(templDecl_);

                        if (la.Kind == DTokens.Identifier || DTokens.BasicTypes[la.Kind])
                        {
                            if (la.Kind == DTokens.Identifier)
                                declStack.Push(new NormalDeclaration(strVal));
                            else
                                declStack.Push(new DTokenDeclaration(la.Kind));
                        }
                        else if (la.Kind == DTokens.OpenParenthesis)
                        {
                            templDecl_.Template = new NormalDeclaration(SkipToClosingParenthesis());
                        }
                        else
                        {
                            SynErr(DTokens.OpenParenthesis, "Expected identifier, base type or '(' when parsing a template initializer");
                            goto do_return;
                        }
                        break;

                    case DTokens.OpenSquareBracket: // int>[<...]
                        declStack.Push(new ArrayDecl(declStack.Pop()));
                        break;

                    case DTokens.Dot: // >.<init
                        if (Peek(1).Kind == DTokens.Dot && Peek().Kind == DTokens.Dot) // >...<
                        {
                            lexer.NextToken(); // 1st dot
                            lexer.NextToken(); // 2nd dot

                            if (declStack.Count < 1) // void foo(>...<) {}
                                declStack.Push(new VarArgDecl());
                            else
                                declStack.Push(new VarArgDecl(declStack.Pop()));
                        }


                        if (Peek(1).Kind != DTokens.Identifier)
                        {
                            SynErr(DTokens.Dot, "Expected identifier after a dot");
                            goto do_return;
                        }

                        if (IsInit) // .init
                        {
                            lexer.NextToken(); // Skip '.'
                            declStack.Push(new NormalDeclaration(strVal));
                        }
                        else // Template!(ABC...)>.<StaticType
                        {
                            declStack.Push(new DotCombinedDeclaration(declStack.Pop()));
                        }
                        break;
                }

                IsInit = IsInMemberModBracket = false;
                lexer.NextToken();
            }

        do_return:

            while (declStack.Count > 1)
            {
                TypeDeclaration innerType = declStack.Pop();
                if (declStack.Peek() is TemplateDecl)
                    (declStack.Peek() as TemplateDecl).Template = innerType;
                else if (declStack.Peek() is ArrayDecl)
                    (declStack.Peek() as ArrayDecl).KeyType = innerType;
                else if (declStack.Peek() is DotCombinedDeclaration)
                    (declStack.Peek() as DotCombinedDeclaration).AccessedMember = innerType;
                else
                    declStack.Peek().Base = innerType;
            }

            if (declStack.Count > 0)
                return declStack.Pop();

            return null;
        }

        /// <summary>
        /// void main(string[] args) {}
        /// void expFunc();
        /// void delegate(int a,bool b) myDelegate;
        /// int i=45;
        /// MyType[] a;
        /// const(char)[] foo();
        /// this() {}
        /// </summary>
        /// <returns></returns>
        DNode ParseExpression()
        {
            DNode tv = new DNode();
            tv.desc = CheckForDocComments();
            tv.StartLocation=la.Location;
            bool isCTor = la.Kind == DTokens.This;
            tv.TypeToken = la.Kind;
            if (!isCTor)
            {
                if (DTokens.Conditions[la.Kind] || DTokens.Conditions[Peek(1).Kind])// b?foo(): bar();
                {
                    SkipToSemicolon();
                    return null;
                }

                tv.Type = ParseTypeIdent(out tv.name);
                if (tv.Type == null) return null;
            }
            else
                tv.Type = new DTokenDeclaration(DTokens.This);

            //if (!isCTor) lexer.NextToken(); // Skip last ID parsed by ParseIdentifier();

            if (IsVarDecl() ||// int foo; TypeTuple!(a,T)[] a;
                (tv.name != null && (la.Kind == DTokens.Semicolon || la.Kind == DTokens.Assign || la.Kind == DTokens.Comma))) // char abc[]>;< dchar def[]>=<...;
            {
                DVariable var = new DVariable();
                var.Assign(tv);
                tv = var;

                if (Peek(1).Kind == DTokens.Assign)
                    lexer.NextToken();

                if (la.Kind == DTokens.Assign)
                    var.Value = ParseAssignIdent(false);

                if (Peek(1).Kind == DTokens.Semicolon || Peek(1).Kind == DTokens.Comma)
                    lexer.NextToken();


                if (la.Kind != DTokens.Comma)
                {
                    if (la.Kind != DTokens.Semicolon)
                    {
                        SynErr(DTokens.Semicolon, "Missing semicolon!");
                        goto expr_ret;
                    }
                }

            }
            else if (la.Kind == DTokens.Identifier || isCTor || (la.Kind == DTokens.CloseParenthesis && Peek(1).Kind == DTokens.OpenParenthesis)) // MyType myfunc() {...}; this()() {...}; int (*foo)>(<int a, bool b);
            {
                DMethod meth = new DMethod();
                meth.Assign(tv);
                tv = meth;
                lexer.NextToken(); // Skip function name

                if (!Expect(DTokens.OpenParenthesis, "Expected '('")) { SkipToClosingBrace(); return null; }

                bool HasTemplateArgs = false;
                #region Scan for template arguments
                int psb = 0;
                DToken pk = la;
                lexer.StartPeek();
                if (pk.Kind == DTokens.OpenParenthesis) psb = -1;
                for (int i = 0; pk != null && pk.Kind != DTokens.EOF; i++)
                {
                    if (pk.Kind == DTokens.OpenParenthesis) psb++;
                    if (pk.Kind == DTokens.CloseParenthesis)
                    {
                        psb--;
                        if (psb < 0)
                        {
                            if (lexer.Peek().Kind == DTokens.OpenParenthesis) HasTemplateArgs = true;
                            break;
                        }
                    }
                    pk = lexer.Peek();
                }
                #endregion

                if (la.Kind != DTokens.CloseParenthesis) // just if some arguments are given!
                {
                    if (HasTemplateArgs)
                        ParseTemplateArguments(ref tv);
                    else
                        ParseFunctionArguments(ref meth);
                }

                if (HasTemplateArgs) // void templFunc(S,T[],U*) (S s, int b=2) {...}
                {
                    if (!Expect(DTokens.CloseParenthesis, "Expected ')'")) { SkipToClosingBrace(); return null; }
                    if (Peek(1).Kind != DTokens.CloseParenthesis) // If there aren't any args, don't try to parse em' :-D
                        ParseFunctionArguments(ref meth);
                    else
                        lexer.NextToken();// Skip "("
                }

                if (!Expect(DTokens.CloseParenthesis, "Expected ')'")) { SkipToClosingBrace(); return null; }

                if (la.Kind == DTokens.Assign) // this() = null;
                {
                    /*tv.value = */
                    SkipToSemicolon();
                    tv.endLoc = GetCodeLocation(la);
                    goto expr_ret;
                }

                #region In|Out|Body regions of a method
                if (DTokens.Modifiers[la.Kind] && la.Kind != DTokens.In && la.Kind != DTokens.Out && la.Kind != DTokens.Body) // void foo() const if(...) {...}
                {
                    lexer.NextToken();
                }

                if (la.Kind == DTokens.If) // void foo() if(...) {}
                {
                    lexer.NextToken(); // Skip "if"
                    SkipToClosingParenthesis();
                    lexer.NextToken(); // Skip ")"
                }

                if (la.Kind == DTokens.Semicolon) { goto expr_ret; } // void foo()();

                if (DTokens.Modifiers[la.Kind] && la.Kind != DTokens.In && la.Kind != DTokens.Out && la.Kind != DTokens.Body) // void foo() const {...}
                {
                    lexer.NextToken();
                }

                if (la.Kind == DTokens.OpenCurlyBrace)// normal function void foo() >{<}
                {
                    Location sloc = tv.StartLocation;
                    ParseBlock(ref tv, true);
                    tv.StartLocation = sloc;
                    goto expr_ret;
                }

            in_out_body:
                if (la.Kind == DTokens.In || la.Kind == DTokens.Out || la.Kind == DTokens.Body) // void foo() in{}body{}
                {

                    if (la.Kind == DTokens.Out)
                    {
                        if (Peek(1).Kind == DTokens.OpenParenthesis)
                        {
                            lexer.NextToken(); // Skip "out"
                            SkipToClosingParenthesis();
                        }
                    }
                    lexer.NextToken(); // Skip "in"

                    Location sloc = tv.StartLocation;
                    ParseBlock(ref tv, true);
                    tv.StartLocation = sloc;

                    DToken bpk = Peek(1);
                    if (bpk.Kind == DTokens.In || bpk.Kind == DTokens.Out || bpk.Kind == DTokens.Body)
                    {
                        lexer.NextToken(); // Skip "}"
                        goto in_out_body;
                    }
                    goto expr_ret;
                }
                #endregion

                SynErr(la.Kind, "unexpected end of function body!");
                return null;
            }
            else
                return null;

        expr_ret:


            return tv;
        }

        /// <summary>
        /// Parses a complete class, template or struct
        /// public class MyType(T,S*,U[]): public Mybase, MyInterface {...}
        /// </summary>
        /// <returns></returns>
        DNode ParseClass()
        {
            DClassLike myc = new DClassLike(); // >class<
            myc.StartLocation = la.Location;
            DNode _myc = myc;
            myc.desc = CheckForDocComments();
            if (la.Kind == DTokens.Struct || la.kind == DTokens.Union) myc.fieldtype = FieldType.Struct;
            if (la.Kind == DTokens.Template) myc.fieldtype = FieldType.Template;
            if (la.Kind == DTokens.Interface) myc.fieldtype = FieldType.Interface;
            myc.TypeToken = la.Kind;
            lexer.NextToken(); // Skip initial type ID ,e.g. "class"

            #region Apply vis modifiers
            bool cvm = DTokens.ContainsVisMod(ExpressionModifiers);
            foreach (int m in BlockModifiers)
            {
                if (!ExpressionModifiers.Contains(m))
                {
                    if (cvm) if (DTokens.VisModifiers[m]) continue;
                    ExpressionModifiers.Add(m);
                }
            }
            myc.modifiers.AddRange(ExpressionModifiers);
            ExpressionModifiers.Clear();
            #endregion

            if (la.Kind != DTokens.Identifier)
            {
                SynErr(DTokens.Identifier, "Identifier required!");
                return null;
            }

            myc.name = strVal; // >MyType<
            LastElement = myc;
            lexer.NextToken(); // Skip id

            if (la.Kind == DTokens.Semicolon) return myc;

            // >(T,S,U[])<
            if (la.Kind == DTokens.OpenParenthesis) // "(" template declaration
            {
                ParseTemplateArguments(ref _myc);
                if (!Expect(DTokens.CloseParenthesis, "Failure during template paramter parsing - \")\" missing!")) { SkipToClosingBrace(); }
            }

            if (myc.name != "Object" && myc.fieldtype != FieldType.Struct) myc.BaseClass = new NormalDeclaration("Object"); // Every object except the Object class itself has "Object" as its base class!
            // >: MyBase, MyInterface< {...}
            if (la.Kind == DTokens.Colon) // : inheritance
            {
                while (!EOF) // Skip modifiers or module paths
                {
                    lexer.NextToken();
                    if (DTokens.Modifiers[la.Kind])
                        continue;// Skip heritage modifier
                    if (Peek(1).Kind == DTokens.Dot)// : std.Class
                        continue;
                    else if (la.Kind != DTokens.Dot)
                        break;
                }
                string _unused = null;
                myc.BaseClass = ParseTypeIdent(out _unused);

                if (Peek(1).Kind == DTokens.Comma) lexer.NextToken();
                if (la.Kind == DTokens.Comma)
                {
                    lexer.NextToken(); // Skip ","
                    myc.ImplementedInterface = ParseTypeIdent(out _unused);
                }
                if (la.Kind != DTokens.OpenCurlyBrace) lexer.NextToken(); // Skip to "{"
            }
            if (myc.BaseClass is NormalDeclaration && (myc.BaseClass as NormalDeclaration).Name == myc.name)
            {
                SemErr(DTokens.Colon, "Cannot inherit \"" + myc.name + "\" from itself!");
                myc.BaseClass = null;
            }

            if (la.Kind == DTokens.If)
            {
                lexer.NextToken();
                SkipToClosingParenthesis();
                lexer.NextToken(); // Skip ")"
            }

            if (la.Kind != DTokens.OpenCurlyBrace)
            {
                SynErr(DTokens.OpenCurlyBrace, "Error parsing " + DTokens.GetTokenString(myc.TypeToken) + " " + myc.name + ": missing {");
                return myc;
            }
            
            ParseBlock(ref _myc, false);

            myc.endLoc = GetCodeLocation(la);

            return myc;
        }

        /// <summary>
        /// Parses an enumeration
        /// enum myType mt=null;
        /// enum ABC:uint {
        /// a=1,
        /// b=23,
        /// c=2,
        /// d,
        /// }
        /// </summary>
        /// <returns></returns>
        DNode ParseEnum()
        {
            DNode mye = new DEnum();
            mye.StartLocation = la.Location;

            mye.Type = new DTokenDeclaration(la.Kind);
            (mye as DEnum).EnumBaseType = new DTokenDeclaration(DTokens.Int);

            #region Apply vis modifiers
            bool cvm = DTokens.ContainsVisMod(ExpressionModifiers);
            foreach (int m in BlockModifiers)
            {
                if (!ExpressionModifiers.Contains(m))
                {
                    if (cvm) if (DTokens.VisModifiers[m]) continue;
                    ExpressionModifiers.Add(m);
                }
            }
            mye.modifiers.AddRange(ExpressionModifiers);
            ExpressionModifiers.Clear();
            #endregion


            if (la.Kind == DTokens.Enum) lexer.NextToken(); // Skip 'enum'

            if (la.Kind != DTokens.OpenCurlyBrace) // Otherwise it would be enum >{<...}
            {
                if (la.Kind != DTokens.Identifier && la.Kind != DTokens.Colon)
                {
                    SynErr(DTokens.Identifier, "Identifier or base type expected after 'enum'!");
                    SkipToClosingBrace();
                    return mye;
                }
                DToken pk = Peek(1);
                if (la.Kind == DTokens.Identifier && (pk.Kind == DTokens.OpenCurlyBrace || pk.Kind == DTokens.Colon)) // enum ABC>{>...}
                {
                    mye.name = strVal;
                    lexer.NextToken(); // Skip enum id
                }

                if (la.Kind == DTokens.Colon) // enum>:<uint | enum ABC>:<uint
                {
                    string _unused = null;
                    (mye as DEnum).EnumBaseType = ParseTypeIdent(out _unused);
                    if (Peek(1).Kind == DTokens.OpenCurlyBrace) lexer.NextToken();
                }
                else if (la.Kind != DTokens.OpenCurlyBrace) // enum Type[]** ABC;
                {
                    DVariable enumVar = new DVariable();
                    enumVar.Assign(mye);
                    mye = enumVar;
                    mye.TypeToken = DTokens.Enum;
                    mye.Type = ParseTypeIdent(out mye.name);

                    if (enumVar.Type == null)
                        enumVar.Type = new DTokenDeclaration(DTokens.Int);

                    enumVar.EndLocation = la.Location;
                    return enumVar;
                }
            }

            if (la.Kind != DTokens.OpenCurlyBrace) // Check beginning "{"
            {
                SynErr(DTokens.OpenCurlyBrace, "Expected '{' when parsing enumeration");
                SkipToClosingBrace();
                mye.EndLocation = la.Location;
                return mye;
            }

            DEnumValue tt = new DEnumValue();
            while (!EOF)
            {
                lexer.NextToken();
            enumcont:
                if (tt == null) tt = new DEnumValue();
                if (ThrowIfEOF(DTokens.CloseCurlyBrace)) break;
                switch (la.Kind)
                {
                    case DTokens.CloseCurlyBrace: // Final "}"
                        if (tt.name != "") mye.Add(tt);
                        mye.EndLocation = la.Location;
                        return mye;
                    case DTokens.Comma: // Next item
                        tt.EndLocation = la.Location;
                        if (tt.name != "") mye.Add(tt);
                        tt = null;
                        break;
                    case DTokens.Assign: // Value assignment
                        tt.Value = ParseAssignIdent(false);
                        if (la.Kind != DTokens.Identifier) goto enumcont;
                        break;
                    case DTokens.Identifier: // Can just be a new item
                        tt.Type = (mye as DEnum).EnumBaseType;
                        tt.StartLocation = la.Location;
                        tt.name = strVal;
                        if (la.Kind != DTokens.Identifier) goto enumcont;
                        break;
                    default: break;
                }
            }
            mye.EndLocation = la.Location;
            return mye;
        }







        #region Error handlers
        public delegate void ErrorHandler(string file, string module, int line, int col, int kindOf, string message);
        static public event ErrorHandler OnError, OnSemanticError;


        void SynErr(int n, int col, int ln)
        {
            OnError(PhysFileName, Document.module, ln, col, n, "");
            //errors.SynErr(ln, col, n);
        }
        void SynErr(int n, int col, int ln, string msg)
        {
            OnError(PhysFileName, Document.module, ln, col, n, msg);
            //errors.Error(ln, col, msg);
        }
        void SynErr(int n, string msg)
        {
            OnError(PhysFileName, Document.module, la.Location.Line, la.Location.Column, n, msg);
            //errors.Error(la.Location.Line, la.Location.Column, msg);
        }
        void SynErr(int n)
        {
            OnError(PhysFileName, Document != null ? Document.module : null, la != null ? la.Location.Line : 0, la != null ? la.Location.Column : 0, n, "");
            //errors.SynErr(la != null ? la.Location.Line : 0, la != null ? la.Location.Column : 0, n);
        }

        void SemErr(int n, int col, int ln)
        {
            OnSemanticError(PhysFileName, Document.module, ln, col, n, "");
            //errors.SemErr(ln, col, n);
        }
        void SemErr(int n, int col, int ln, string msg)
        {
            OnSemanticError(PhysFileName, Document.module, ln, col, n, msg);
            //errors.Error(ln, col, msg);
        }
        void SemErr(int n, string msg)
        {
            OnSemanticError(PhysFileName, Document.module, la.Location.Line, la.Location.Column, n, msg);
            //errors.Error(la.Location.Line, la.Location.Column, msg);
        }
        void SemErr(int n)
        {
            OnSemanticError(PhysFileName, Document != null ? Document.module : null, la != null ? la.Location.Line : 0, la != null ? la.Location.Column : 0, n, "");
            //errors.SemErr(la != null ? la.Location.Line : 0, la != null ? la.Location.Column : 0, n);
        }
        #endregion
    }
}
