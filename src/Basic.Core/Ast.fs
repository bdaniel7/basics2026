namespace Basic.Core

module Ast =

    type UnaryOp =
        | Neg
        | Not

    type BinaryOp =
        | Add
        | Sub
        | Mul
        | Div
        | Mod
        | Eq
        | Ne
        | Lt
        | Le
        | Gt
        | Ge
        | And
        | Or

    type Expr =
        | Number of float
        | Str of string
        | Var of string
        | Unary of UnaryOp * Expr
        | Binary of BinaryOp * Expr * Expr
        | Call of string * Expr list

    type PrintElement =
        | PExpr of Expr
        | PSemi
        | PComma
        | PAt of Expr * Expr
        | PTab of Expr
        | PInk of Expr
        | PPaper of Expr
        | POver of Expr
        | PInverse of Expr
        | PBorder of Expr

    type Stmt =
        | Let of string * Expr
        | Print of PrintElement list
        | Input of string list
        | If of Expr * Stmt list * Stmt list option
        | For of string * Expr * Expr * Expr option
        | Next of string option
        | Goto of Expr
        | Gosub of Expr
        | Return
        | Run of Expr option
        | Randomize of Expr option
        | Dim of (string * Expr list) list
        | Rem of string
        | Stop
        | End
        | Pause of Expr option
        | Cls
        | Plot of Expr * Expr
        | Draw of Expr * Expr
        | Circle of Expr * Expr * Expr
        | Ink of Expr
        | Paper of Expr
        | Border of Expr
        | Over of Expr
        | Inverse of Expr
        | At of Expr * Expr
        | Beep of Expr * Expr
        | Sound of Expr * Expr
        | Nop

    type Line =
        { Number: int
          Statements: Stmt list }

    type Program =
        { Lines: Line list }
