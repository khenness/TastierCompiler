.names 4
.proc Main
.proc forTest
.var 2 b
.var 1 i
forTest: Enter 2
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
Const 0
Sto 0 1
Jmp L$3
L$4: Nop
Load 0 1
Const 10
Lss
Load 0 1
Const 1
Add
Sto 0 1
FJmp L$5
L$3: Nop
Load 0 0
Write
Load 0 1
Write
Jmp L$4
L$5: Nop
Jmp L$1
L$2: Nop
Leave
Ret
Main: Enter 0
Call 1 forTest
Leave
Ret
