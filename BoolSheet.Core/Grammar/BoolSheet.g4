// BoolSheet expression grammar (lab variant 30).
// Operators: + - * /   mod div   = < >   <= >= <>   not
// Precedence, from lowest to highest:
//   1) not          (in front of a whole expression: "not A1 > 2" = not (A1 > 2))
//   2) comparison   = <> < > <= >=   (not chainable: "1 < 2 < 3" is a syntax error)
//   3) additive     + -
//   4) multiplicative  * / mod div
//   5) number, cell reference, (expression), not operand
//      (inside a comparison or arithmetic "not" applies to one operand: "A1 = not B1")

grammar BoolSheet;

options { caseInsensitive = true; }   // "not" = "NOT" = "Not", "a1" = "A1"

// ---------- Parser rules ----------

formula        : expression EOF ;

expression     : NOT expression                         # NotExpr
               | comparison                             # CompareExpr
               ;

comparison     : additive ((EQ | NEQ | LE | GE | LT | GT) additive)? ;

additive       : multiplicative ((PLUS | MINUS) multiplicative)* ;

multiplicative : atom ((MUL | SLASH | MOD | DIV) atom)* ;

atom           : NUMBER                                 # NumberAtom
               | CELL                                   # CellAtom
               | LPAREN expression RPAREN               # ParenAtom
               | NOT atom                               # NotAtom
               ;

// ---------- Lexer rules ----------

NOT    : 'not' ;
MOD    : 'mod' ;
DIV    : 'div' ;

NEQ    : '<>' ;
LE     : '<=' ;
GE     : '>=' ;
EQ     : '=' ;
LT     : '<' ;
GT     : '>' ;

PLUS   : '+' ;
MINUS  : '-' ;
MUL    : '*' ;
SLASH  : '/' ;

LPAREN : '(' ;
RPAREN : ')' ;

// One letter only: the sheet has at most 26 columns (A-Z). A multi-letter rule would also swallow
// keywords written without a space: "mod3" or "not1" would be read as cell addresses.
CELL   : [A-Z] [0-9]+ ;      // A1, B12, Z100
NUMBER : [0-9]+ ;            // arbitrary-length integer, parsed into BigInteger

WS     : [ \t\r\n]+ -> skip ;
