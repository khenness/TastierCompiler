.names 4
.proc Main
.proc SumUp
.var 2 b
.var 1 i
SumUp: Enter 2
Const 0
Sto 0 0
L$0: Nop
LoadG 3
Const 0
Gtr
FJmp L$1
Load 0 0
LoadG 3
Add
Sto 0 0
LoadG 3
Const 1
Sub
StoG 3
Jmp L$0
L$1: Nop
Load 0 0
Write
Const 9999
Sto 0 1
L$2: Nop
Load 0 1
Const 1
Equ
FJmp L$3
Load 0 1
Write
Jmp L$2
L$3: Nop
Leave
Ret
Main: Enter 0
Read
StoG 3
L$4: Nop
LoadG 3
Const 0
Gtr
FJmp L$5
Call 1 SumUp
Read
StoG 3
Jmp L$4
L$5: Nop
Leave
Ret
