.names 2
.proc Main
.proc ex6Test
ex6Test: Enter 4
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
Load 0 0
Write
L$0: Nop
Jmp L$1
L$2: Nop
Const 111
Sto 0 1
Const 999
Sto 0 2
Const 2
Const 1
Gtr
FJmp L$3
Load 0 1
Sto 0 3
Jmp L$4
L$3: Nop
Load 0 2
Sto 0 3
L$4: Nop
Load 0 3
Write
Const 2
Const 1
Lss
FJmp L$5
Load 0 1
Sto 0 3
Jmp L$6
L$5: Nop
Load 0 2
Sto 0 3
L$6: Nop
Load 0 3
Write
Leave
Ret
Main: Enter 0
Call 1 ex6Test
Leave
Ret
