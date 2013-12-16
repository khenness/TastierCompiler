.names 4
.proc Main
.proc ex6Test
.var 1 atestintthatisglobal
.var 2 atestboolthatisglobal
ex6Test: Enter 5
Jmp L$0
ex6Test$sillyinternalmethod: Enter 2
Const 8888
Sto 0 1
Load 0 1
Write
Leave
Ret
L$0: Nop
Call 0 ex6Test$sillyinternalmethod
Const 0
Sto 0 1
Jmp L$1
L$2: Nop
Load 0 1
Const 10
Lss
Load 0 1
Const 1
Add
Sto 0 1
FJmp L$3
Load 0 1
Write
L$1: Nop
Jmp L$2
L$3: Nop
Const 111
Sto 0 2
Const 999
Sto 0 3
Const 2
Const 1
Gtr
FJmp L$4
Load 0 2
Sto 0 4
Jmp L$5
L$4: Nop
Load 0 3
Sto 0 4
L$5: Nop
Load 0 4
Write
Const 2
Const 1
Lss
FJmp L$6
Load 0 2
Sto 0 4
Jmp L$7
L$6: Nop
Load 0 3
Sto 0 4
L$7: Nop
Load 0 4
Write
Leave
Ret
Main: Enter 0
Call 1 ex6Test
Leave
Ret
