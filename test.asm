.names 4
.proc Main
.proc forTest
.var 2 b
.var 1 i
forTest: Enter 1
Const 0
Sto 0 0
Const 0
Sto 0 0
L$0: Nop
Load 0 0
Const 10
Lss
Load 0 0
Const 1
Add
Sto 0 0
FJmp L$1
Load 0 0
Write
Jmp L$0
L$1: Nop
Leave
Ret
Main: Enter 0
Call 1 forTest
Leave
Ret
