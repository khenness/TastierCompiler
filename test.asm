<<<<<<< HEAD
.names 1
.proc Main
Main: Enter 3
Const 1
Sto 0 0
Const 2
Sto 0 1
Const 3
Sto 0 2
Const 1
Const 0
Neq
FJmp L$0
Load 0 0
Write
Jmp L$1
L$0: Nop
L$1: Nop
Const 1
Const 1
Leq
FJmp L$2
Load 0 1
Write
Jmp L$3
L$2: Nop
L$3: Nop
Const 1
Const 1
Geq
FJmp L$4
Load 0 2
Write
Jmp L$5
L$4: Nop
L$5: Nop
=======
.names 4
.proc Main
.proc SumUp
.var 2 b
.var 1 i
SumUp: Enter 1
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
Leave
Ret
Main: Enter 0
Read
StoG 3
L$2: Nop
LoadG 3
Const 0
Gtr
FJmp L$3
Call 1 SumUp
Read
StoG 3
Jmp L$2
L$3: Nop
>>>>>>> 0f539a1531e767e453eac49523d5f4a196a70870
Leave
Ret
