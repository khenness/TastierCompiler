
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using Symbol = System.Tuple<string, int, int, int, int>;
using Instruction = System.Tuple<string,string>;
using StackAddress = System.Tuple<int, int>;
using StructMember = System.Tuple<int, int>; /*type, kind*/
using ArraySymbol = System.Tuple<string, string, int>; /*name, type (as a string - for structs the structname, 
                                                           else int or bool), dimensions*/
using FuncSymbol = System.Tuple<string, int, string>; /*name, parameters, returntype*/


namespace Tastier {



public class Parser {
	public const int _EOF = 0;
	public const int _number = 1;
	public const int _ident = 2;
	public const int maxT = 46;

	const bool T = true;
	const bool x = false;
	const int minErrDist = 2;

	public Scanner scanner;
	public Errors  errors;

	public Token t;    // last recognized token
	public Token la;   // lookahead token
	int errDist = minErrDist;

enum TastierType : int {   // types for variables
    Undefined,
    Integer,
    Boolean
  };

  enum TastierKind : int {  // kinds of symbol
    Const,
    Var,
    Struct,
    Proc,
    Array
  };

/*
  You'll notice some type aliases, such as the one just below, are commented
  out. This is because C# only allows using-alias-directives outside of a
  class, while class-inheritance directives are allowed inside. So the
  snippet immediately below is illegal in here. To complicate matters
  further, the C# runtime does not properly handle class-inheritance
  directives for Tuples (it forces you to write some useless methods). For
  these reasons, the type aliases which alias Tuples can be found in
  Parser.frame, but they're documented in this file, with the rest.
*/

  //using Symbol = System.Tuple<string, int, int, int, int>;

/*
  A Symbol is a name with a type and a kind. The first int in the
  tuple is the kind, and the second int is the type. We'll use these to
  represent declared names in the program.

  For each Symbol which is a variable, we have to allocate some storage, so
  the variable lives at some address in memory. The address of a variable on
  the stack at runtime has two components. The first component is which
  stack frame it's in, relative to the current procedure. If the variable is
  declared in the procedure that's currently executing, then it will be in
  that procedure's stack frame. If it's declared in the procedure that
  called the currently active one, then it'll be in the caller's stack
  frame, and so on. The first component is the offset that says how many
  frames up the chain of procedure calls to look for the variable. The
  second component is simply the location of the variable in the stack frame
  where it lives.

  The third int in the symbol is the stack frame on which the variable
  lives, and the fourth int is the index in that stack frame. Since
  variables which are declared in the global scope aren't inside any
  function, they don't have a stack frame to go into. In this compiler, our
  convention is to put these variables at an address in the data memory. If
  the variable was declared in the global scope, the fourth field in the
  Symbol will be zero, and we know that the next field is an address in
  global memory, not on the stack.

  Procedures, on the other hand, are just sets of instructions. A procedure
  is not data, so it isn't stored on the stack or in memory, but is just a
  particular part of the list of instructions in the program being run. If
  the symbol is the name of a procedure, we'll store a -1 in the address
  field (5).

  When the program is being run, the code will be loaded into the machine's
  instruction memory, and the procedure will have an address there. However,
  it's easier for us to just give the procedure a unique label, instead of
  remembering what address it lives at. The assembler will take care of
  converting the label into an address when it encounters a JMP, FJMP or
  CALL instruction with that label as a target.

  To summarize:
    * Symbol.Item1 -> name
    * Symbol.Item2 -> kind
    * Symbol.Item3 -> type
    * Symbol.Item4 -> stack frame pointer
    * Symbol.Item5 -> variable's address in the stack frame pointed to by
                      Item4, -1 if procedure
*/

  class Scope : Stack<Symbol> {}

/*
  A scope contains a stack of symbol definitions. Every time we come across
  a new local variable declaration, we can just push it onto the stack. We'll
  use the position of the variable in the stack to represent its address in
  the stack frame of the procedure in which it is defined. In other words, the
  variable at the bottom of the stack goes at location 0 in the stack frame,
  the next variable at location 1, and so on.
*/

  //using Instruction = Tuple<string, string>;
  class Program : List<Instruction> {}

/*
  A program is just a list of instructions. When the program is loaded into
  the machine's instruction memory, the instructions will be laid out in the
  same order that they appear in this list. Because of this, we can use the
  location of an instruction in the list as its address in instruction memory.
  Labels are just names for particular locations in the list of instructions
  that make up the program.

  The first component of all instructions is a label, which can be empty.
  The second component is the actual instruction itself.

  To summarize:
    * Instruction.Item1 -> label
    * Instruction.Item2 -> the actual instruction, as a string
*/

/* struct code */
                            
Dictionary<string, Dictionary<string, StructMember>> definedStructs = new Dictionary<string, Dictionary<string, StructMember>>();
  

/*arry code - for convenience we will keep a list of the names of all arrays declared in the program*/
List<ArraySymbol> declaredArrays = new List<ArraySymbol>();

/*storing extra info about functions*/
List<FuncSymbol> declaredFuncs = new List<FuncSymbol>();

/*passing in parameters*/
Stack<Instruction> PassIn = new Stack<Instruction>();

Stack<Scope> openScopes = new Stack<Scope>();
Scope externalDeclarations = new Scope();

/*
  Every time we encounter a new procedure declaration in the program, we want
  to make sure that expressions inside the procedure see all of the variables
  that were in scope at the point where the procedure was defined. We also
  want to make sure that expressions outside the procedure do not see the
  procedure's local variables. Every time we encounter a procedure, we'll push
  a new scope on the stack of open scopes. When the procedure ends, we can pop
  it off and continue, knowing that the local variables defined in the
  procedure cannot be seen outside, since we've popped the scope which
  contains them off the stack.
*/

Program program = new Program();
Program header = new Program();

Stack<string> openProcedureDeclarations = new Stack<string>();

/*
  In order to implement the "shadowing" of global procedures by local procedures
  properly, we need to generate a label for local procedures that is different
  from the label given to procedures of the same name in outer scopes. See the
  test case program "procedure-label-shadowing.TAS" for an example of why this
  is important. In order to make labels unique, when we encounter a non-global
  procedure declaration called "foo" (for example), we'll give it the label
  "enclosingProcedureName$foo" for all enclosing procedures. So if it's at
  nesting level 2, it'll get the label "outermost$nextoutermost$foo". Let's
  make a function that does this label generation given the set of open
  procedures which enclose some new procedure name.
*/

string generateProcedureName(string name) {
  if (openProcedureDeclarations.Count == 0) {
    return name;
  } else {
    string temp = name;
    foreach (string s in openProcedureDeclarations) {
      temp = s + "$" + temp;
    }
    return temp;
  }
}

/*
  We also need a function that figures out, when we call a procedure from some
  scope, what label to call. This is where we actually implement the shadowing;
  the innermost procedure with that name should be called, so we have to figure
  out what the label for that procedure is.
*/

string getLabelForProcedureName(int lexicalLevelDifference, string name) {
  /*
     We want to skip <lexicalLevelDifference> labels backwards, but compose
     a label that incorporates the names of all the enclosing procedures up
     to that point. A lexical level difference of zero indicates a procedure
     defined in the current scope; a difference of 1 indicates a procedure
     defined in the enclosing scope, and so on.
  */
  int numOpenProcedures = openProcedureDeclarations.Count;
  int numNamesToUse = (numOpenProcedures - lexicalLevelDifference);
  string theLabel = name;

  /*
    We need to concatenate the first <numNamesToUse> labels with a "$" to
    get the name of the label we need to call.
  */

  var names = openProcedureDeclarations.Take(numNamesToUse);

  foreach (string s in names) {
      theLabel = s + "$" + theLabel;
  }

  return theLabel;
}

Stack<string> openLabels = new Stack<string>();
int labelSeed = 0;

string generateLabel() {
  return "L$"+labelSeed++;
}

/*
  Sometimes, we need to jump over a block of code which we're about to
  generate (for example, at the start of a loop, if the test fails, we have
  to jump to the end of the loop). Because it hasn't been generated yet, we
  don't know how long it will be (in the case of the loop, we don't know how
  many instructions will be in the loop body until we actually generate the
  code, and count them). In this case, we can make up a new label for "the
  end of the loop" and emit a jump to that label. When we get to the end of
  the loop, we can put the label in, so that the jump will go to the
  labelled location. Since we can have loops within loops, we need to keep
  track of which label is the one that we are currently trying to jump to,
  and we need to make sure they go in the right order. We'll use a stack to
  store the labels for all of the forward jumps which are active. Every time
  we need to do a forward jump, we'll generate a label, emit a jump to that
  label, and push it on the stack. When we get to the end of the loop, we'll
  put the label in, and pop it off the stack.
*/

Symbol _lookup(Scope scope, string name) {
  foreach (Symbol s in scope) {
      if (s.Item1 == name) {
        return s;
      }
  }
  return null;
}

Symbol lookup(Stack<Scope> scopes, string name) {
  int stackFrameOffset = 0;
  int variableOffset = 0;

  foreach (Scope scope in scopes) {
    foreach (Symbol s in scope) {
      if (s.Item1 == name ) {
        return s;
      }
      else {
        variableOffset += 1;
      }
    }
    stackFrameOffset += 1;
    variableOffset = 0;
  }
  return null; // if the name wasn't found in any open scopes.
}

/*
  You may notice that when we use a LoadG or StoG instruction, we add 3 to
  the address of the item being loaded or stored. This is because the
  control and status registers of the machine are mapped in at addresses 0,
  1, and 2 in data memory, so we cannot use those locations for storing
  variables. If you want to load rtp, rbp, or rpc onto the stack to
  manipulate them, you can LoadG and StoG to those locations.
*/

/*--------------------------------------------------------------------------*/



	public Parser(Scanner scanner) {
		this.scanner = scanner;
		errors = new Errors();
	}

	void SynErr (int n) {
		if (errDist >= minErrDist) errors.SynErr(la.line, la.col, n);
		errDist = 0;
	}

	public void SemErr (string msg) {
		if (errDist >= minErrDist) errors.SemErr(t.line, t.col, msg);
		errDist = 0;
	}

  public void Warn (string msg) {
    Console.WriteLine("-- line " + t.line + " col " + t.col + ": " + msg);
  }

  public void Write (string filename) {
    List<string> output = new List<string>();
    foreach (Instruction i in header) {
      if (i.Item1 != "") {
        output.Add(i.Item1 + ": " + i.Item2);
      } else {
        output.Add(i.Item2);
      }
    }
    File.WriteAllLines(filename, output.ToArray());
  }

	void Get () {
		for (;;) {
			t = la;
			la = scanner.Scan();
			if (la.kind <= maxT) { ++errDist; break; }

			la = t;
		}
	}

	void Expect (int n) {
		if (la.kind==n) Get(); else { SynErr(n); }
	}

	bool StartOf (int s) {
		return set[s, la.kind];
	}

	void ExpectWeak (int n, int follow) {
		if (la.kind == n) Get();
		else {
			SynErr(n);
			while (!StartOf(follow)) Get();
		}
	}


	bool WeakSeparator(int n, int syFol, int repFol) {
		int kind = la.kind;
		if (kind == n) {Get(); return true;}
		else if (StartOf(repFol)) {return false;}
		else {
			SynErr(n);
			while (!(set[syFol, kind] || set[repFol, kind] || set[0, kind])) {
				Get();
				kind = la.kind;
			}
			return StartOf(syFol);
		}
	}


	void AddOp(out Instruction inst) {
		inst = new Instruction("", "Add"); 
		if (la.kind == 3) {
			Get();
		} else if (la.kind == 4) {
			Get();
			inst = new Instruction("", "Sub"); 
		} else SynErr(47);
	}

	void Expr(out TastierType type, string varname) {
		string varname1 ="";  TastierType type1; Instruction inst; 
		SimExpr(out type, varname);
		if (StartOf(1)) {
			RelOp(out inst);
			SimExpr(out type1, varname1);
			if (type != type1) {
			 SemErr("incompatible types");
			}
			else {
			 program.Add(inst);
			 type = TastierType.Boolean;
			}
			
		}
	}

	void SimExpr(out TastierType type, string varname) {
		string varname1=""; TastierType type1; Instruction inst; 
		Term(out type, varname);
		while (la.kind == 3 || la.kind == 4) {
			AddOp(out inst);
			Term(out type1, varname1);
			if (type != TastierType.Integer || type1 != TastierType.Integer) {
			 SemErr("integer type expected");
			}
			program.Add(inst);
			
		}
	}

	void RelOp(out Instruction inst) {
		inst = new Instruction("", "Equ"); 
		switch (la.kind) {
		case 16: {
			Get();
			break;
		}
		case 17: {
			Get();
			inst = new Instruction("", "Lss"); 
			break;
		}
		case 18: {
			Get();
			inst = new Instruction("", "Gtr"); 
			break;
		}
		case 19: {
			Get();
			inst = new Instruction("", "Neq"); 
			break;
		}
		case 20: {
			Get();
			inst = new Instruction("", "Leq"); 
			break;
		}
		case 21: {
			Get();
			inst = new Instruction("", "Geq"); 
			break;
		}
		default: SynErr(48); break;
		}
	}

	void Factor(out TastierType type, string varname) {
		string varname1 =""; int n; Symbol sym; string name; 
		type = TastierType.Undefined; 
		if (la.kind == 2) {
			Ident(out name);
			bool isExternal = false; //CS3071 students can ignore external declarations, since they only deal with compilation of single files.
			sym = lookup(openScopes, name);
			if (sym == null) {
			 sym = _lookup(externalDeclarations, name);
			 isExternal = true;
			}
			
			if (sym == null) {
			 SemErr("reference to undefined variable " + name+"  [Factor nonterminal]");
			}
			else {
			 type = (TastierType)sym.Item3;
			 if ((TastierKind)sym.Item2 == TastierKind.Var) {
			   if (sym.Item4 == 0) {
			       if (isExternal) {
			         program.Add(new Instruction("", "LoadG " + sym.Item1));
			         // if the symbol is external, we load it by name. The linker will resolve the name to an address.
			       } else {
			         program.Add(new Instruction("", "LoadG " + (sym.Item5+3)));
			          //here
			
			       }
			   } else {
			       int lexicalLevelDifference = Math.Abs(openScopes.Count - sym.Item4)-1;
			       program.Add(new Instruction("", "Load " + lexicalLevelDifference + " " + sym.Item5));
			         //here
			
			    }
			 } else SemErr("variable expected");
			}
			
		} else if (la.kind == 1) {
			Get();
			n = Convert.ToInt32(t.val);
			program.Add(new Instruction("", "Const " + n));
			type = TastierType.Integer;
			
		} else if (la.kind == 4) {
			Get();
			Factor(out type, varname1);
			if (type != TastierType.Integer) {
			 SemErr("integer type expected");
			 type = TastierType.Integer;
			}
			program.Add(new Instruction("", "Neg"));
			program.Add(new Instruction("", "Const 1"));
			program.Add(new Instruction("", "Add"));
			
		} else if (la.kind == 5) {
			Get();
			program.Add(new Instruction("", "Const " + 1)); type = TastierType.Boolean; 
		} else if (la.kind == 6) {
			Get();
			program.Add(new Instruction("", "Const " + 0)); type = TastierType.Boolean; 
		} else SynErr(49);
	}

	void Ident(out string name) {
		Expect(2);
		name = t.val; 
	}

	void MulOp(out Instruction inst) {
		inst = new Instruction("", "Mul"); 
		if (la.kind == 7) {
			Get();
		} else if (la.kind == 8) {
			Get();
			inst = new Instruction("", "Div"); 
		} else SynErr(50);
	}

	void ProcDecl() {
		Symbol mySym; int parameters=0; string returntypestring="";  string name1; 
		TastierType returntype = TastierType.Undefined; string name; string label;
		Scope currentScope = openScopes.Peek(); int enterInstLocation = 0;
		bool external = false; 
		TastierType mytype = TastierType.Undefined;
		
		Expect(9);
		if (la.kind == 10) {
			Get();
		} else if (la.kind == 41 || la.kind == 42) {
			Type(out returntype);
		} else SynErr(51);
		if(returntype == TastierType.Undefined ){
		   returntypestring="void";
		}else if(returntype == TastierType.Integer){
		   returntypestring="int";
		}else if(returntype == TastierType.Boolean){
		   returntypestring="bool";
		
		}
		
		
		
		Ident(out name);
		currentScope.Push(new Symbol(name, (int)TastierKind.Proc, (int)TastierType.Undefined, openScopes.Count, -1));
		openScopes.Push(new Scope());
		currentScope = openScopes.Peek();
		program.Add(new Instruction("", "Enter 0"));
		enterInstLocation = program.Count - 1;
		label = generateProcedureName(name);
		openProcedureDeclarations.Push(name);
		
		
		
		Expect(11);
		if (la.kind == 41 || la.kind == 42) {
			Type(out mytype);
			Ident(out name1);
			parameters++; 
			mySym = new Symbol(name1, (int)TastierKind.Var,(int)mytype, openScopes.Count-1,currentScope.Count(s => s.Item2 == (int)TastierKind.Const || s.Item2 == (int)TastierKind.Var));
			currentScope.Push(mySym );
			Console.WriteLine("Function Parameter declared. (name = "+ mySym.Item1+", kind = "+mySym.Item2+", type = "+mySym.Item3+", stackframe = "+mySym.Item4+", stackframeoffset = "+mySym.Item5+")");
			
			
			while (la.kind == 12) {
				Get();
				Type(out mytype);
				Ident(out name1);
				parameters++;
				mySym = new Symbol(name1, (int)TastierKind.Var,(int)mytype,openScopes.Count-1,currentScope.Count(s => s.Item2 == (int)TastierKind.Const || s.Item2 == (int)TastierKind.Var));
				currentScope.Push(mySym );
				Console.WriteLine("Function Parameter declared. (name = "+ mySym.Item1+", kind = "+mySym.Item2+", type = "+mySym.Item3+", stackframe = "+mySym.Item4+", stackframeoffset = "+mySym.Item5+")");
				
				
			}
		}
		Expect(13);
		declaredFuncs.Add(new FuncSymbol(name, parameters, returntypestring));
		/*pass in function values*/
		  Instruction IS;
		while(PassIn.Count != 0){
		   IS = PassIn.Pop();
		  string i = IS.Item1;
		  string i1 = IS.Item2;
		 // program.Add(new Instruction(i, i1));
		}
		
		
		Expect(14);
		
		while (StartOf(2)) {
			if (la.kind == 41 || la.kind == 42) {
				VarDecl(external);
			} else if (la.kind == 22) {
				StructDecl(external);
			} else if (StartOf(3)) {
				Stat();
			} else {
				openLabels.Push(generateLabel());
				program.Add(new Instruction("", "Jmp " + openLabels.Peek()));
				/*
				 We need to jump over procedure
				 definitions because otherwise we'll
				 execute all the code inside them!
				 Procedures should only be entered via
				 a Call instruction.
				*/
				
				ProcDecl();
				program.Add(new Instruction(openLabels.Pop(), "Nop")); 
			}
		}
		Expect(15);
		program.Add(new Instruction("", "Leave"));
		program.Add(new Instruction("", "Ret"));
		openScopes.Pop();
		// now we can generate the Enter instruction properly
		program[enterInstLocation] =
		 new Instruction(label, "Enter " + currentScope.Count(s => s.Item2 == (int)TastierKind.Const || s.Item2 == (int)TastierKind.Var));
		openProcedureDeclarations.Pop();
		
	}

	void Type(out TastierType type) {
		type = TastierType.Undefined; 
		if (la.kind == 41) {
			Get();
			type = TastierType.Integer; 
		} else if (la.kind == 42) {
			Get();
			type = TastierType.Boolean; 
		} else SynErr(52);
	}

	void VarDecl(bool external) {
		int dimensions; string name; TastierType type; Scope currentScope = openScopes.Peek();
		
		Type(out type);
		if (la.kind == 24) {
			int n; int total =1; Queue<int> numElementsQueue = new Queue<int>();
			string indexname;  
			
			Get();
			dimensions = 1; 
			Expect(1);
			n = Convert.ToInt32(t.val) ;
			total = total * (n+1);
			numElementsQueue.Enqueue(n);
			
			Expect(25);
			while (la.kind == 24) {
				Get();
				Expect(1);
				dimensions = dimensions+1;
				n = Convert.ToInt32(t.val);
				total = total * (n+1);   //total is the number of elements we will generate.
				numElementsQueue.Enqueue(n);
				
				Expect(25);
			}
			Ident(out name);
			Expect(23);
			string typestring = "undefined";
			if(type == TastierType.Integer){
			 typestring = "int";
			}else if(type == TastierType.Boolean){
			 typestring = "bool";
			}
			//string will go here.
			
			
			/*does array exist already?*/
			   //We are keeping a list of arrays declared in the program.
			
			  foreach(ArraySymbol AS in declaredArrays){
			      if(AS.Item1 == name && AS.Item2 == typestring && AS.Item3 == dimensions){
			         SemErr("ERROR - Array '"+name+"' already defined");
			      }
			   }
			declaredArrays.Add(new ArraySymbol(name, typestring, dimensions));
			Console.WriteLine("Array '"+name+"' declared. Type = '"+typestring+"' Number of dimensions = "+dimensions+".");
			
			
			
			
			
			/*Algorithim to generate all array members*/
			Dictionary<int, string> Dict = new Dictionary<int, string>();
			for (int i =0; i< total; i++){
			  Dict.Add(i, name);
			}     
			int beforeincrement = total;
			for(int i =0; i< dimensions; i++){
			    
			    int mynum = numElementsQueue.Dequeue();
			     beforeincrement = (beforeincrement / (mynum+1));
			    int writevalue = 0;
			    int count =0;
			    Console.WriteLine(">   beforeincrement = "+beforeincrement+", mynum = "+mynum+"");
			   // Dict[0] =  Dict[0] + "["+writevalue+"]";
			
			    for(int j =0; j< total; j++){
			       if(count == beforeincrement){
			          writevalue++;
			          writevalue = writevalue % (mynum+1);
			          count = 0;
			       } 
			       count++;
			
			       Dict[j] =  Dict[j] + "["+writevalue+"]";
			
			    }
			}
			for(int i =0; i< total; i++){
			 /*make a new symbol*/
			Symbol mySym = new Symbol(Dict[i], (int)TastierKind.Var, (int)type, openScopes.Count-1,currentScope.Count(s => s.Item2 == (int)TastierKind.Const || s.Item2 == (int)TastierKind.Var));
			Console.WriteLine("     Variable "+ mySym.Item1+" declared. = (kind = "+mySym.Item2+ ", type = "+mySym.Item3+", stackframe = "+mySym.Item4+", stackframeoffset = "+mySym.Item5+"),");
			currentScope.Push(mySym);
			}
			Console.WriteLine(")");
			
		} else if (la.kind == 2) {
			Ident(out name);
			foreach(char c in name){
			  if(c == '.'){
			    SemErr("Cannot use the dot operator when declaring a variable.");
			  }
			}
			foreach(char c in name){
			  if(c == '[' | c == ']'){
			    SemErr("Cannot use the '[' or ']' charactar  when declaring a variable.");
			  }
			}
			
			if (external) {
			 Symbol mySym = new Symbol(name, (int)TastierKind.Var, (int)type, 0, 0);
			 externalDeclarations.Push(mySym);
			 Console.WriteLine("Variable declared. (name = "+ mySym.Item1+",kind = "+mySym.Item2+ ", type = "+mySym.Item3+", stackframe = "+mySym.Item4+", stackframeoffset = "+mySym.Item5+")");
			} else {
			Symbol mySym = new Symbol(name, (int)TastierKind.Var,(int)type, openScopes.Count-1,currentScope.Count(s => s.Item2 == (int)TastierKind.Const || s.Item2 == (int)TastierKind.Var));
			currentScope.Push(mySym );
			 Console.WriteLine("Variable declared. (name = "+ mySym.Item1+", kind = "+mySym.Item2+", type = "+mySym.Item3+", stackframe = "+mySym.Item4+", stackframeoffset = "+mySym.Item5+")");
			
			}
			
			Expect(23);
		} else SynErr(53);
	}

	void StructDecl(bool external) {
		string structname2; int n; int total =1; Queue<int> numElementsQueue = new Queue<int>();  string structname; int dimensions = 0; Scope currentScope = openScopes.Peek(); string instancename; TastierKind kind = TastierKind.Var;  bool structnameUnique = true;  string membername; TastierType type = TastierType.Undefined;
		
		Expect(22);
		Ident(out structname);
		if (la.kind == 24) {
			Get();
			dimensions = 1; 
			Expect(1);
			if(definedStructs.ContainsKey(structname) == false){
			  SemErr("ERROR - struct '"+structname+"' has not been defined.");
			}
			
			n = Convert.ToInt32(t.val) ;
			total = total * (n+1);  //total is the total number of elements we will generate    
			numElementsQueue.Enqueue(n);
			
			Expect(25);
			while (la.kind == 24) {
				Get();
				dimensions = dimensions+1;
				Expect(1);
				n = Convert.ToInt32(t.val);
				total = total * (n+1);   //total is the number of elements we will generate.
				numElementsQueue.Enqueue(n);
				
				Expect(25);
			}
			Ident(out instancename);
			Expect(23);
			string structstring = "struct_"+structname;
			//We are keeping a list of arrays declared in the program.
			foreach(ArraySymbol AS in declaredArrays){
			  if(AS.Item1 == instancename && AS.Item2 == structstring && AS.Item3 == dimensions){
			     SemErr("ERROR - Array already defined");
			  }
			}
			ArraySymbol newAS = new ArraySymbol(instancename, structstring, dimensions);
			declaredArrays.Add(newAS);
			Console.WriteLine("Struct Array '"+instancename+"' declared. Type = '"+newAS.Item2+"' Number of dimensions = "+dimensions+". Members(");
			
			
			
			/*Algorithim to generate all array members*/
			Dictionary<int, string> Dict = new Dictionary<int, string>();
			for (int i =0; i< total; i++){
			Dict.Add(i, instancename);
			}     
			int beforeincrement = total;
			for(int i =0; i< dimensions; i++){
			
			int mynum = numElementsQueue.Dequeue();
			 beforeincrement = (beforeincrement / (mynum+1));
			int writevalue = 0;
			int count =0;
			Console.WriteLine(">   beforeincrement = "+beforeincrement+", mynum = "+mynum+"");
			// Dict[0] =  Dict[0] + "["+writevalue+"]";
			
			for(int j =0; j< total; j++){
			   if(count == beforeincrement){
			      writevalue++;
			      writevalue = writevalue % (mynum+1);
			      count = 0;
			   } 
			   count++;
			
			   Dict[j] =  Dict[j] + "["+writevalue+"]";
			
			}
			}
			// TastierType type = TastierType.Undefined;
			for(int i =0; i< total; i++){
			/*make a new symbol*/
			foreach (KeyValuePair<string, StructMember> kvp in definedStructs[structname]){
			  Symbol mySym = new Symbol(Dict[i]+"."+kvp.Key, (int)TastierKind.Var, kvp.Value.Item1, openScopes.Count-1,currentScope.Count(s => s.Item2 == (int)TastierKind.Const || s.Item2 == (int)TastierKind.Var));
			   Console.WriteLine("     Variable '"+ mySym.Item1+"' declared. = (kind = "+mySym.Item2+ ", type = "+mySym.Item3+", sframe = "+mySym.Item4+", offset = "+mySym.Item5+"),");
			    currentScope.Push(mySym);
			}
			// Console.WriteLine("    "+Dict[i]);
			}
			Console.WriteLine(")");
			
			
			
			
		} else if (la.kind == 2) {
			Ident(out instancename);
			Expect(23);
			if(definedStructs.ContainsKey(structname) == true){
			Console.WriteLine("Struct '"+instancename+"' of type '"+structname+"' instantiated. (");
			  foreach(KeyValuePair<string, StructMember> kvp in definedStructs[structname]){
			     string symbolname = instancename + "." +kvp.Key;
			     Symbol mySym = new Symbol(symbolname, kvp.Value.Item2,kvp.Value.Item1, openScopes.Count-1,currentScope.Count(s => s.Item2 == (int)TastierKind.Const || s.Item2 == (int)TastierKind.Var));
			   //  Console.WriteLine("   Variable "+ mySym.Item1+" declared. ");
			       Console.WriteLine("   Variable "+ mySym.Item1+" declared. (kind = "+mySym.Item2+ ", type = "+mySym.Item3+", stackframe = "+mySym.Item4+", stackframeoffset = "+mySym.Item5+")");
			    // Console.WriteLine("    (kind = "+mySym.Item2+ ", type = "+mySym.Item3+", stackframe = "+mySym.Item4+", stackframeoffset = "+mySym.Item5+")");
			     currentScope.Push(mySym);
			
			  }
			  Console.WriteLine(")");
			}else{
			   SemErr("ERROR - struct '"+structname+"' has not been defined.");
			}
			
			
			
		} else if (la.kind == 14) {
			Get();
			structnameUnique = true;
			foreach (KeyValuePair<string, Dictionary<string, StructMember>> VarList in definedStructs){
			  if(VarList.Key == structname){
			    SemErr("ERROR - struct '"+structname+"' is already defined.");
			  }
			}
			definedStructs.Add(structname, new Dictionary<string,StructMember>());
			     
			
			if (la.kind == 22) {
				InternalStruct(structname);
			} else if (la.kind == 41 || la.kind == 42) {
				Type(out type);
				Ident(out membername);
				kind = TastierKind.Var;                                          
				definedStructs[structname].Add(membername, new StructMember((int)type, (int)kind));   
				
				
				Expect(23);
			} else SynErr(54);
			while (la.kind == 22 || la.kind == 41 || la.kind == 42) {
				if (la.kind == 22) {
					InternalStruct(structname);
				} else {
					Type(out type);
					Ident(out membername);
					kind = TastierKind.Var;
					definedStructs[structname].Add(membername, new StructMember((int)type, (int)kind));
					
					Expect(23);
				}
			}
			Expect(26);
			Console.WriteLine("Struct '"+structname+"' defined.  Attributes=(");
			foreach (KeyValuePair<string, StructMember> kvp in definedStructs[structname]){
			  Console.WriteLine("    "+kvp.Key+", type = "+kvp.Value.Item1+", kind = "+kvp.Value.Item2+",");
			}
			Console.WriteLine( ")" );
			
		} else SynErr(55);
	}

	void Stat() {
		string varname = ""; string newname; TastierType type; TastierType typeA; TastierType type1;  TastierType typeB;  string name =""; Symbol sym; bool external = false; bool isExternal = false; 
		switch (la.kind) {
		case 2: {
			Ident(out name);
			sym = lookup(openScopes, name);
			if (sym == null) {
			 sym = _lookup(externalDeclarations, name);
			 isExternal = true;
			}
			if (sym == null) {
			 SemErr("reference to undefined variable " + name);
			}
			
			if (la.kind == 27) {
				Get();
				if ((TastierKind)sym.Item2 != TastierKind.Var) {
				 SemErr("cannot assign to non-variable");
				}
				
				Expr(out type, name);
				if (la.kind == 23) {
					Get();
					if (type != (TastierType)sym.Item3) {
					 SemErr("incompatible types");  //=
					}
					if (sym.Item4 == 0) {
					 if (isExternal) {
					   program.Add(new Instruction("", "StoG " + sym.Item1));
					   // if the symbol is external, we also store it by name. The linker will resolve the name to an address.
					 } else {
					   //passin
					   program.Add(new Instruction("", "StoG " + (sym.Item5+3)));
					 }
					}
					else {
					 int lexicalLevelDifference = Math.Abs(openScopes.Count - sym.Item4)-1; //=
					 program.Add(new Instruction("", "Sto " + lexicalLevelDifference + " " + sym.Item5));
					 
					
					}
					
				} else if (la.kind == 28) {
					Get();
					if ((TastierType)type != TastierType.Boolean) {
					  SemErr("boolean type expected for conditional assignment");
					}
					
					// if false, jump to elselabel. otherwise continue as normal
					string elselabel = generateLabel();
					program.Add(new Instruction("", "FJmp " + elselabel));
					string endlabel = generateLabel();
					
					
					
					Expr(out typeA, varname);
					Expect(29);
					if (typeA != (TastierType)sym.Item3) {
					    SemErr("incompatible types");  //=
					  }
					  if (sym.Item4 == 0) {
					    if (isExternal) {
					      program.Add(new Instruction("", "StoG " + sym.Item1));
					      // if the symbol is external, we also store it by name. The linker will resolve the $
					    } else {
					      program.Add(new Instruction("", "StoG " + (sym.Item5+3)));
					    }
					  }
					  else {
					    int lexicalLevelDifference = Math.Abs(openScopes.Count - sym.Item4)-1; //=
					    program.Add(new Instruction("", "Sto " + lexicalLevelDifference + " " + sym.Item5));
					  }
					
					
					//Jmp endlabel 
					program.Add(new Instruction("", "Jmp " + endlabel));
					program.Add(new Instruction(elselabel, "Nop"));
					
					
					Expr(out typeB, varname);
					Expect(23);
					if (typeB != (TastierType)sym.Item3) {
					    SemErr("incompatible types");  //=
					  }
					  if (sym.Item4 == 0) {
					    if (isExternal) {
					    //  program.Add(new Instruction("", "StoG " + sym.Item1));
					      // if the symbol is external, we also store it by name. The linker will resolve the $
					    } else {
					      program.Add(new Instruction("", "StoG " + (sym.Item5+3)));
					    }
					  }
					  else {
					    int lexicalLevelDifference = Math.Abs(openScopes.Count - sym.Item4)-1; //=
					    program.Add(new Instruction("", "Sto " + lexicalLevelDifference + " " + sym.Item5));
					  }
					
					
					
					//endlabel
					program.Add(new Instruction(endlabel, "Nop"));
					
					
				} else SynErr(56);
			} else if (la.kind == 11) {
				Get();
				int parameters = 0; 
				while (StartOf(4)) {
					Expr(out type,  varname);
					parameters++;
					Symbol sym1 = lookup(openScopes, name);
					if (sym1 == null) {
					sym1 = _lookup(externalDeclarations, name);
					isExternal = true;
					}
					if (sym1 == null) {
					SemErr("reference to undefined variable " + name);
					}
					
					int lexicalLevelDifference2 = Math.Abs(openScopes.Count - sym1.Item4);
					PassIn.Push(new Instruction("", "StoG " + (sym1.Item5+3)));
					PassIn.Push(new Instruction("", "Load " + lexicalLevelDifference2 + " " + sym1.Item5));
					
					
					
				}
				Expect(13);
				Expect(23);
				bool valid = false;
				foreach(FuncSymbol FS in declaredFuncs){
				  if(FS.Item1 == name && FS.Item2 == parameters){
				    valid = true;
				  }
				}
				if(valid == false){
				   SemErr("wrong no. of parameters");
				}
				if ((TastierKind)sym.Item2 != TastierKind.Proc) {
				  SemErr("object is not a procedure");
				}
				
				int currentStackLevel = openScopes.Count;
				int lexicalLevelDifference = Math.Abs(openScopes.Count - sym.Item4);
				string procedureLabel = getLabelForProcedureName(lexicalLevelDifference, sym.Item1);
				program.Add(new Instruction("", "Call " + lexicalLevelDifference + " " + procedureLabel));
				
			} else SynErr(57);
			break;
		}
		case 30: {
			Get();
			Expect(11);
			Expr(out type,  varname);
			Expect(13);
			if ((TastierType)type != TastierType.Boolean) {
			 SemErr("boolean type expected");
			}
			openLabels.Push(generateLabel());
			program.Add(new Instruction("", "FJmp " + openLabels.Peek()));
			
			Stat();
			Instruction startOfElse = new Instruction(openLabels.Pop(), "Nop");
			/*
			  If we got into the "if", we need to
			  jump over the "else" so that it
			  doesn't get executed.
			*/
			openLabels.Push(generateLabel());
			program.Add(new Instruction("", "Jmp " + openLabels.Peek()));
			program.Add(startOfElse);
			
			if (la.kind == 31) {
				Get();
				Stat();
			}
			program.Add(new Instruction(openLabels.Pop(), "Nop")); 
			break;
		}
		case 32: {
			Get();
			Expect(11);
			Expr(out type,  varname);
			Expect(13);
			Expect(14);
			openLabels.Push(generateLabel());
			
			while (la.kind == 33) {
				Get();
				Expr(out type1,  varname);
				Expect(29);
				if (type != type1){ 
				  SemErr("incompatible types in switch statement");
				}
				openLabels.Push(generateLabel());
				program.Add(new Instruction("", "Equ"));
				program.Add(new Instruction("", "FJmp " + openLabels.Peek())); 
				
				Stat();
				if (StartOf(5)) {
					while (la.kind == 22 || la.kind == 41 || la.kind == 42) {
						if (la.kind == 41 || la.kind == 42) {
							VarDecl(external);
						} else {
							StructDecl(external);
						}
					}
				}
				string label = openLabels.Pop();
				program.Add(new Instruction("", "Jmp " + openLabels.Peek() ));
				program.Add(new Instruction(label, "Nop"));
				
				
				Expect(34);
			}
			if (la.kind == 35) {
				Get();
				Expect(29);
				Stat();
				if (StartOf(5)) {
					while (la.kind == 22 || la.kind == 41 || la.kind == 42) {
						if (la.kind == 41 || la.kind == 42) {
							VarDecl(external);
						} else {
							StructDecl(external);
						}
					}
				}
				Expect(34);
			}
			Expect(15);
			program.Add(new Instruction(openLabels.Pop(), "Nop"));  
			break;
		}
		case 36: {
			Get();
			string loopStartLabel = generateLabel();
			openLabels.Push(generateLabel()); //second label is for the loop end
			program.Add(new Instruction(loopStartLabel, "Nop"));
			
			Expect(11);
			Expr(out type,  varname);
			Expect(13);
			if ((TastierType)type != TastierType.Boolean) {
			 SemErr("boolean type expected");
			}
			program.Add(new Instruction("", "FJmp " + openLabels.Peek())); // jump to the loop end label if condition is false
			
			Stat();
			program.Add(new Instruction("", "Jmp " + loopStartLabel));
			program.Add(new Instruction(openLabels.Pop(), "Nop")); // put the loop end label here
			
			break;
		}
		case 37: {
			Get();
			Expect(11);
			SimpleAssignment();
			string CompareStartLabel = generateLabel(); 
			program.Add(new Instruction("", "Jmp " + CompareStartLabel)); //There was some attempted trickery here with assembly language loops. This jump does literally nothing now.
			string loopStartLabel = generateLabel();
			openLabels.Push(generateLabel()); //second label is for the loop end
			program.Add(new Instruction(loopStartLabel, "Nop"));
			
			Expr(out type, varname);
			SimpleAssignment();
			Expect(13);
			if ((TastierType)type != TastierType.Boolean) {
			 SemErr("boolean type expected");
			}
			// program.Add(new Instruction(CompareStartLabel, "Nop"));
			program.Add(new Instruction("", "FJmp " + openLabels.Peek())); // jump to the loop end l$
			//  program.Add(new Instruction(CompareStartLabel, "Nop"));
			
			
			
			Stat();
			program.Add(new Instruction(CompareStartLabel, "Nop"));          
			program.Add(new Instruction("", "Jmp " + loopStartLabel));
			program.Add(new Instruction(openLabels.Pop(), "Nop")); // put the loop end label here
			
			break;
		}
		case 38: {
			Get();
			Ident(out name);
			Expect(23);
			sym = lookup(openScopes, name);
			if (sym == null) {
			 sym = _lookup(externalDeclarations, name);
			 isExternal = true;
			}
			if (sym == null) {
			 SemErr("reference to undefined variable " + name);
			}
			
			if (sym.Item2 != (int)TastierKind.Var) {
			 SemErr("variable type expected but " + sym.Item1 + " has kind " + (TastierType)sym.Item2);
			}
			
			if (sym.Item3 != (int)TastierType.Integer) {
			 SemErr("integer type expected but " + sym.Item1 + " has type " + (TastierType)sym.Item2);
			}
			program.Add(new Instruction("", "Read"));
			
			if (sym.Item4 == 0) {
			 if (isExternal) {
			   program.Add(new Instruction("", "StoG " + sym.Item1));
			   // if the symbol is external, we also store it by name. The linker will resolve the name to an address.
			 } else {
			   program.Add(new Instruction("", "StoG " + (sym.Item5+3)));
			 }
			}
			else {
			 int lexicalLevelDifference = Math.Abs(openScopes.Count - sym.Item4)-1;
			 program.Add(new Instruction("", "Sto " + lexicalLevelDifference + " " + sym.Item5));
			}
			
			break;
		}
		case 39: {
			Get();
			Expr(out type, varname);
			if (type != TastierType.Integer) {
			 SemErr("integer type expected");
			}
			program.Add(new Instruction("", "Write"));
			
			while (la.kind == 12) {
				Get();
				Expr(out type,  varname);
				if (type != TastierType.Integer) {
				 SemErr("integer type expected");
				}
				program.Add(new Instruction("", "Write"));
				
			}
			Expect(23);
			break;
		}
		case 14: {
			Get();
			while (StartOf(6)) {
				if (StartOf(3)) {
					Stat();
				} else if (la.kind == 22) {
					StructDecl(external);
				} else {
					VarDecl(external);
				}
			}
			Expect(15);
			break;
		}
		default: SynErr(58); break;
		}
	}

	void Term(out TastierType type, string varname) {
		string varname1 =""; TastierType type1; Instruction inst; 
		Factor(out type, varname);
		while (la.kind == 7 || la.kind == 8) {
			MulOp(out inst);
			Factor(out type1, varname1);
			if (type != TastierType.Integer ||
			   type1 != TastierType.Integer) {
			 SemErr("integer type expected");
			}
			program.Add(inst);
			
		}
	}

	void InternalStruct(string parentstruct) {
		TastierType type; TastierKind kind; string structname; string instancename; 
		Expect(22);
		Ident(out structname);
		Ident(out instancename);
		Expect(23);
		if(parentstruct == structname){
		
		SemErr("ERROR - you have a '"+ structname+"' struct inside its own definition! ");
		}
		
		bool valid = false;
		foreach (KeyValuePair<string, Dictionary<string, StructMember>> VarList in definedStructs){
		  if(VarList.Key == structname){
		          valid = true;
		  }
		}
		if(valid == false){
		  SemErr("ERROR - struct '"+structname+"'"+" has not been defined.");
		}
		
		//add all nested members to the parent. eg: owner.age; owner.birthday.day;    
		foreach (KeyValuePair<string, StructMember> kvp in definedStructs[structname]){
		  string membername =  instancename + "." + kvp.Key;
		  definedStructs[parentstruct].Add(membername, new StructMember(kvp.Value.Item1, kvp.Value.Item2));
		
		}
		
		
		
	}

	void SimpleAssignment() {
		string varname=""; string newname; TastierType type; string name; Symbol sym; bool external = false;  bool isExternal = false; 
		Ident(out name);
		sym = lookup(openScopes, name);
		if (sym == null) {
		 sym = _lookup(externalDeclarations, name);
		 isExternal = true;
		}
		if (sym == null) {
		 SemErr("reference to undefined variable " + name);
		}
		
		Expect(27);
		if ((TastierKind)sym.Item2 != TastierKind.Var) {
		 SemErr("cannot assign to non-variable");
		}
		
		Expr(out type,  varname);
		Expect(23);
		if (type != (TastierType)sym.Item3) {
		 SemErr("incompatible types");  //=
		}
		if (sym.Item4 == 0) {
		 if (isExternal) {
		   program.Add(new Instruction("", "StoG " + sym.Item1));
		   // if the symbol is external, we also store it by name. The linker will resolve the $
		 } else {
		   program.Add(new Instruction("", "StoG " + (sym.Item5+3)));
		 }
		}
		else {
		 int lexicalLevelDifference = Math.Abs(openScopes.Count - sym.Item4)-1;
		 program.Add(new Instruction("", "Sto " + lexicalLevelDifference + " " + sym.Item5));
		}
		
	}

	void Tastier() {
		string name; bool external = false; 
		Expect(40);
		Ident(out name);
		Console.WriteLine("");
		Console.WriteLine("");
		Console.WriteLine("===================");
		Console.WriteLine("< Program begins >");
		
		//                                   Console.WriteLine("Debugging log: ");
		Console.WriteLine("===================");
		
		Console.WriteLine("");
		
		openScopes.Push(new Scope());
		
		Expect(14);
		while (StartOf(7)) {
			if (la.kind == 22) {
				StructDecl(external);
			} else if (la.kind == 41 || la.kind == 42) {
				VarDecl(external);
			} else if (la.kind == 9) {
				ProcDecl();
			} else if (la.kind == 44) {
				ExternDecl();
			} else {
				ConstDecl(external);
			}
		}
		Expect(15);
		if (openScopes.Peek().Count == 0) {
		 Warn("Warning: Program " + name + " is empty ");
		}
		
		header.Add(new Instruction("", ".names " + (externalDeclarations.Count + openScopes.Peek().Count)));
		//modified
		Console.WriteLine("\n");
		foreach (Symbol s in openScopes.Peek()) {
		
		 if (s.Item2 == (int)TastierKind.Var) {
		    if(s.Item3 == 1){
		       //found int
		      Console.WriteLine("There is a global variable '" + s.Item1+ "' of type int. (s.Item2= "+ s.Item2+", s.Item3= "+ s.Item3+")");
		      Console.WriteLine("Stack frame pointer of '"+ s.Item1+"' is " +s.Item4+", variables address in the stack frame pointer is " + s.Item5+".\n");   
		    }else if(s.Item3 == 2){
		        //found bool
		        Console.WriteLine("There is a global variable '"+ s.Item1+ "' of type bool. (s.Item2= "+ s.Item2+", s.Item3= "+ s.Item3+")");
		        Console.WriteLine("Stack frame pointer of '"+ s.Item1+"' is " + s.Item4+", variables address in the stack frame pointer is " + s.Item5+".\n");
		      }else{
		     //found undefined
		       Console.WriteLine("There is a global variable " + s.Item1+ "that has an undefined type.");
		       Console.WriteLine("Stack frame pointer of this: " + s.Item4+", variables address in the stack frame pointer: " + s.Item5+".\n");  
		  }
		
		   header.Add(new Instruction("", ".var " + ((int)s.Item3) + " " + s.Item1));
		 } else if (s.Item2 == (int)TastierKind.Proc) {
		   Console.WriteLine("There is a procedure in the program called  '" + s.Item1+"'.");
		   Console.WriteLine("Stack frame pointer of '"+ s.Item1+"' is " + s.Item4+ ", starting address in the generated assembly code is given by the label '"+s.Item1+":'.\n");
		   header.Add(new Instruction("", ".proc " + s.Item1));
		  /* foreach (Symbol s1 in openScopes.Peek()) {
		     if(s1.Item3 == 1){
		       //found int
		       Console.WriteLine("There is a local variable '" + s1.Item1+ "' of type int.");
		       
		     }else if(s1.Item3 == 2){
		       //found bool
		       Console.WriteLine("There is a local variable '"+ s1.Item1+ "' of type bool.");
		       
		     }else{
		       //found undefined
		       Console.WriteLine("There is a local variable " + s1.Item1+ "that has an undefined type.");
		       
		     }
		   }
		  */
		 } else if (s.Item2 == (int)TastierKind.Const) {
		   // Console.WriteLine("I know its a const called " + s.Item1 + "");
		      if(s.Item3 == 1){
		       //found int
		        Console.WriteLine("There is a const  '" + s.Item1+ "' of type int.  Variables address in the stack frame pointer is " + s.Item5+".\n");
		
		
		     }else if(s.Item3 == 2){
		       //found bool
		       Console.WriteLine("There is a const  '" + s.Item1+ "' of type bool.  Variables address in the stack frame pointer is " + s.Item5+".\n");
		
		     }else{
		       //found undefined
		       Console.WriteLine("There is a const " + s.Item1+ "that has an undefined type.");
		
		     }
		
		 }else{
		    SemErr("global item " + s.Item1 + " has no defined type. It has a type of "+ s.Item2);
		  }
		}
		//modified
		foreach (Symbol s in externalDeclarations ) {
		  // Console.WriteLine("Looking at something in externalDeclarations");
		     if(s.Item3 == 1){
		       //found int
		       Console.WriteLine("There is a const  '" + s.Item1+ "' of type int.  Variables address in the stack frame pointer is " + s.Item5+".\n");
		
		     }else if(s.Item3 == 2){
		       //found bool
		        Console.WriteLine("There is a const  '" + s.Item1+ "' of type bool. Variables address in the stack frame pointer is " + s.Item5+".\n");
		     }else{
		       //found undefined
		       Console.WriteLine("There is a const " + s.Item1+ "that has an undefined type.");
		
		     }
		
		 if (s.Item2 == (int)TastierKind.Var) {
		   header.Add(new Instruction("", ".external var " + ((int)s.Item3) + " " + s.Item1));
		 } else if (s.Item2 == (int)TastierKind.Proc) {
		   header.Add(new Instruction("", ".external proc " + s.Item1));
		 } else if(s.Item2 == (int)TastierKind.Const){
		   //modified
		   header.Add(new Instruction("", ".external const " + ((int)s.Item3) + " " + s.Item1));
		 } else {
		   SemErr("external item " + s.Item1 + " has no defined type");
		 }
		}
		header.AddRange(program);
		openScopes.Pop();
		
		if(errors.count == 0){
		Console.WriteLine();
		Console.WriteLine("=============================");
		
		
		Console.WriteLine("< Program ends>");
		Console.WriteLine("=============================");
		
		 Console.WriteLine("COMPILATION SUCESSFUL! there were 0 errors detected!");
		
		Console.WriteLine();
		Console.WriteLine("To run simple.TAS:");
		
		Console.WriteLine("=============================");
		
		//                                    Console.WriteLine("To run simple.TAS:");
		Console.WriteLine("mono bin/tcc.exe test/Programs/simple.TAS test.asm");
		Console.WriteLine("tasm test.asm test.bc");
		Console.WriteLine("tvm test.bc test/Inputs/test.IN");
		Console.WriteLine("=============================");
		//  Console.WriteLine();
		//  Console.WriteLine("COMPILATION SUCESSFUL! there were 0 errors detected!");
		 Console.WriteLine();
		
		}else{
		// Console.WriteLine(arg[0]);
		 Console.WriteLine("COMPILATION FAILED!  "+errors.count+" error(s) detected. "); 
		}
		
		
	}

	void ExternDecl() {
		string name; bool external = true; Scope currentScope = openScopes.Peek(); int count = currentScope.Count; 
		Expect(44);
		if (la.kind == 41 || la.kind == 42) {
			VarDecl(external);
		} else if (la.kind == 45) {
			Get();
			Ident(out name);
			Expect(23);
			externalDeclarations.Push(new Symbol(name, (int)TastierKind.Proc, (int)TastierType.Undefined, 1, -1)); 
		} else SynErr(59);
	}

	void ConstDecl(bool external) {
		string varname =""; string name; TastierType type; Scope currentScope = openScopes.Peek(); Symbol sym; 
		
		Expect(43);
		Type(out type);
		Ident(out name);
		sym = lookup(openScopes, name);
		
		sym = new Symbol(name,(int)TastierKind.Const,(int)type, 0,currentScope.Count(s => s.Item2 == (int)TastierKind.Var || s.Item2 == (int)TastierKind.Const));
		currentScope.Push(sym);
		
		//  }
		
		Expect(27);
		if ((TastierKind)sym.Item2 != TastierKind.Const) {
		 SemErr("cannot assign to non-variable");
		}
		
		Expr(out type,  varname);
		if (type != (TastierType)sym.Item3) {
		 SemErr("incompatible types");
		}
		if (sym.Item4 == 0) {
		   program.Add(new Instruction("", "StoG " + (sym.Item5+3)));
		}
		else {
		 int lexicalLevelDifference = Math.Abs(openScopes.Count - sym.Item4)-1;
		 program.Add(new Instruction("", "Sto " + lexicalLevelDifference + " " + sym.Item5));
		}
		
		while (la.kind == 12) {
			Get();
			Type(out type);
			Ident(out name);
			sym = new Symbol(name, (int)TastierKind.Const,(int)type,0,currentScope.Count(s => s.Item2 == (int)TastierKind.Const || s.Item2 == (int)TastierKind.Var));
			currentScope.Push(sym);
			
			
			
			Expect(27);
			if ((TastierKind)sym.Item2 != TastierKind.Const) {
			 SemErr("cannot assign to non-variable");
			}
			
			Expr(out type, varname);
			if (type != (TastierType)sym.Item3) {
			 SemErr("incompatible types");
			}
			if (sym.Item4 == 0) {
			// if (isExternal) {
			  // program.Add(new Instruction("", "StoG " + sym.Item1));
			   // if the symbol is external, we also store it by name. The linker will resolve the name to an address.
			// } else {
			   program.Add(new Instruction("", "StoG " + (sym.Item5+3)));
			//}
			}
			else {
			 int lexicalLevelDifference = Math.Abs(openScopes.Count - sym.Item4)-1;
			 program.Add(new Instruction("", "Sto " + lexicalLevelDifference + " " + sym.Item5));
			}
			
		}
		Expect(23);
	}



	public void Parse() {
		la = new Token();
		la.val = "";
		Get();
		Tastier();
		Expect(0);

	}

	static readonly bool[,] set = {
		{T,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, T,T,T,T, T,T,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
		{x,x,T,x, x,x,x,x, x,T,x,x, x,x,T,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,T,x, T,x,x,x, T,T,T,T, x,T,T,x, x,x,x,x},
		{x,x,T,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, T,x,x,x, T,T,T,T, x,x,x,x, x,x,x,x},
		{x,T,T,x, T,T,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x},
		{x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,T,T,x, x,x,x,x},
		{x,x,T,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,T,x, T,x,x,x, T,T,T,T, x,T,T,x, x,x,x,x},
		{x,x,x,x, x,x,x,x, x,T,x,x, x,x,x,x, x,x,x,x, x,x,T,x, x,x,x,x, x,x,x,x, x,x,x,x, x,x,x,x, x,T,T,T, T,x,x,x}

	};
} // end Parser


public class Errors {
	public int count = 0;                                    // number of errors detected
	public System.IO.TextWriter errorStream = Console.Out;   // error messages go to this stream
	public string errMsgFormat = "-- line {0} col {1}: {2}"; // 0=line, 1=column, 2=text

	public virtual void SynErr (int line, int col, int n) {
		string s;
		switch (n) {
			case 0: s = "EOF expected"; break;
			case 1: s = "number expected"; break;
			case 2: s = "ident expected"; break;
			case 3: s = "\"+\" expected"; break;
			case 4: s = "\"-\" expected"; break;
			case 5: s = "\"true\" expected"; break;
			case 6: s = "\"false\" expected"; break;
			case 7: s = "\"*\" expected"; break;
			case 8: s = "\"/\" expected"; break;
			case 9: s = "\"_\" expected"; break;
			case 10: s = "\"void\" expected"; break;
			case 11: s = "\"(\" expected"; break;
			case 12: s = "\",\" expected"; break;
			case 13: s = "\")\" expected"; break;
			case 14: s = "\"{\" expected"; break;
			case 15: s = "\"}\" expected"; break;
			case 16: s = "\"=\" expected"; break;
			case 17: s = "\"<\" expected"; break;
			case 18: s = "\">\" expected"; break;
			case 19: s = "\"!=\" expected"; break;
			case 20: s = "\"<=\" expected"; break;
			case 21: s = "\">=\" expected"; break;
			case 22: s = "\"struct\" expected"; break;
			case 23: s = "\";\" expected"; break;
			case 24: s = "\"[\" expected"; break;
			case 25: s = "\"]\" expected"; break;
			case 26: s = "\"};\" expected"; break;
			case 27: s = "\":=\" expected"; break;
			case 28: s = "\"?\" expected"; break;
			case 29: s = "\":\" expected"; break;
			case 30: s = "\"if\" expected"; break;
			case 31: s = "\"else\" expected"; break;
			case 32: s = "\"switch\" expected"; break;
			case 33: s = "\"case\" expected"; break;
			case 34: s = "\"break;\" expected"; break;
			case 35: s = "\"default\" expected"; break;
			case 36: s = "\"while\" expected"; break;
			case 37: s = "\"for\" expected"; break;
			case 38: s = "\"read\" expected"; break;
			case 39: s = "\"write\" expected"; break;
			case 40: s = "\"program\" expected"; break;
			case 41: s = "\"int\" expected"; break;
			case 42: s = "\"bool\" expected"; break;
			case 43: s = "\"const\" expected"; break;
			case 44: s = "\"external\" expected"; break;
			case 45: s = "\"procedure\" expected"; break;
			case 46: s = "??? expected"; break;
			case 47: s = "invalid AddOp"; break;
			case 48: s = "invalid RelOp"; break;
			case 49: s = "invalid Factor"; break;
			case 50: s = "invalid MulOp"; break;
			case 51: s = "invalid ProcDecl"; break;
			case 52: s = "invalid Type"; break;
			case 53: s = "invalid VarDecl"; break;
			case 54: s = "invalid StructDecl"; break;
			case 55: s = "invalid StructDecl"; break;
			case 56: s = "invalid Stat"; break;
			case 57: s = "invalid Stat"; break;
			case 58: s = "invalid Stat"; break;
			case 59: s = "invalid ExternDecl"; break;

			default: s = "error " + n; break;
		}
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}

	public virtual void SemErr (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}

	public virtual void SemErr (string s) {
		errorStream.WriteLine(s);
		count++;
	}

	public virtual void Warning (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
	}

	public virtual void Warning(string s) {
		errorStream.WriteLine(s);
	}
} // Errors


public class FatalError: Exception {
	public FatalError(string m): base(m) {}
}
}