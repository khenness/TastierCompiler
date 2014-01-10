.names 2
.proc Main
.proc myfun
myfun: Enter 1
Load 0 0
Write
Leave
Ret
Main: Enter 60
Const 21
Sto 0 0
Load 0 0
Const 0
Equ
FJmp L$1
Const 101
Sto 0 1
Jmp L$0
L$1: Nop
Const 1
Equ
FJmp L$2
Const 102
Sto 0 1
Jmp L$0
L$2: Nop
Const 103
Sto 0 1
L$0: Nop
Load 0 1
Write
Const 22
Load 0 40
Add
Sto 0 6
Load 0 6
Write
Const 32
Sto 0 2
Load 0 2
Load 0 2
Add
Sto 0 2
Load 0 2
Write
Const 41
Sto 0 1
Load 0 1
Call 1 myfun
Leave
Ret
