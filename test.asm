.names 3
.proc Main
.proc forTest
.var 2 b
forTest: Enter 5
Const 0
Sto 0 1
Const 0
Sto 0 0
Const 0
Sto 0 0
Jmp L$0
L$1: Nop
Load 0 0
Const 10
Lss
Load 0 0
Const 1
Add
Sto 0 0
FJmp L$2
L$0: Nop
Jmp L$1
L$2: Nop
Const 0
Sto 0 2
Const 1
Sto 0 3
Const 2
Sto 0 4
Const 3
Const 1
Gtr
FJmp L$3
Const 100
Sto 0 4
Jmp L$4
L$3: Nop
Const 200
Sto 0 4
L$4: Nop
Load 0 4
Write
Leave
Ret
Main: Enter 0
Call 1 forTest
Leave
Ret
