﻿open System

exception MyException of string
type MyDelegate = delegate of int -> int
let f x y = ()
let array = [|0|]
let mutable mutableLet = 0

// OK
let _ = fun x -> DateTime.FromBinary x
let _ = fun x -> (fun y -> f DateTime.Now) x
let _ = fun x -> (f 1) x
let _ = fun x -> f Int64 x
let _ = fun x -> f None x
let _ = fun x y -> f mutableLet y

// NOT OK
let _ = fun x -> f mutableLet x
let _ = fun x -> DateTime.FromBinary(1).AddSeconds x
let _ = fun x -> f [for i in 0 .. 10 -> 5 ] x
let _ = fun x -> (if true then DateTime.Now.AddSeconds else DateTime.FromOADate) x
let _ = fun x -> (if true then failwith "" else DateTime.FromBinary) x
let _ = fun x -> (if true then raise (MyException("")) else DateTime.FromBinary) x
let _ = fun x -> MyDelegate(fun (x: int) -> x).Invoke x
let _ = fun x -> f Seq.empty x
let _ = fun x -> f (f 1 2) x
let _ = fun x -> [1; 2; 3].Item x
let _ = fun x -> "".Chars x
let _ = fun x -> f [DateTime.Now] x
let _ = fun x -> f [0; 1].[0] x
let _ = fun x -> f array[0] x
let _ = fun x -> f (Some(5)) x
let _ = fun x -> f (5 |> Some) x
let _ = fun x -> f (1 |> f 2) x
let _ = fun x -> f (Int64()) x
let _ = fun x -> f (new Int64()) x
let _ = fun x -> DateTime.Kek x
