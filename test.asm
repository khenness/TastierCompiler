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
Leave
Ret
