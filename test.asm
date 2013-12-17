.names 2
.proc Main
.proc ex7Test
ex7Test: Enter 7
Load 0 1
Write
Const 0
Sto 0 3
Jmp L$0
L$1: Nop
Load 0 3
Const 10
Lss
Load 0 3
Const 1
Add
Sto 0 3
FJmp L$2
Load 0 3
Write
L$0: Nop
Jmp L$1
L$2: Nop
Const 111
Sto 0 4
Const 999
Sto 0 5
Const 2
Const 1
Gtr
FJmp L$3
Load 0 4
Sto 0 6
Jmp L$4
L$3: Nop
Load 0 5
Sto 0 6
L$4: Nop
Load 0 6
Write
Const 2
Const 1
Lss
FJmp L$5
Load 0 4
Sto 0 6
Jmp L$6
L$5: Nop
Load 0 5
Sto 0 6
L$6: Nop
Load 0 6
Write
Leave
Ret
Main: Enter 1
Load 0 0
Write
Call 1 ex7Test
Leave
Ret
