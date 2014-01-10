.names 2
.proc Main
.proc myfun
myfun: Enter 1
Const 999
Sto 0 0
Load 0 0
Write
Leave
Ret
Main: Enter 6
Const 1
Sto 0 0
Load 0 0
Const 101
Sto 0 1
L$0: Nop
Load 0 1
Write
Call 1 myfun
Leave
Ret
