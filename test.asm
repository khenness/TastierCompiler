.names 2
.proc Main
.proc ex7Test
ex7Test: Enter 6
Const 0
Sto 0 2
Jmp L$0
L$1: Nop
Load 0 2
Const 10
Lss
Load 0 2
Const 1
Add
Sto 0 2
FJmp L$2
Load 0 2
Write
L$0: Nop
Jmp L$1
L$2: Nop
Const 111
Sto 0 3
Const 999
Sto 0 4
Const 2
Const 1
Gtr
FJmp L$3
Load 0 3
Sto 0 5
Jmp L$4
L$3: Nop
Load 0 4
Sto 0 5
L$4: Nop
Load 0 5
Write
Const 2
Const 1
Lss
FJmp L$5
Load 0 3
Sto 0 5
Jmp L$6
L$5: Nop
Load 0 4
Sto 0 5
L$6: Nop
Load 0 5
Write
Leave
Ret
Main: Enter 6
Const 73
Sto 0 0
Load 0 0
Write
Const 21
Sto 0 3
Load 0 3
Write
Load 0 3
Load 0 3
Add
Const 1
Add
Sto 0 3
Load 0 3
Write
Call 1 ex7Test
Leave
Ret
