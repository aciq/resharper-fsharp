//${ARGS_OCCURRENCE:(fun x y -> true) 1 2}

type B = B of bool

B (fun x y -> true) 1{caret} 2
