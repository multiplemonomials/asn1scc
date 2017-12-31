﻿module DAstEqual
open System
open System.Numerics
open System.IO

open FsUtils
open CommonTypes
open DAst
open DAstUtilFunctions


// TODO
// 1 In sequences with default components, default value must be taken into account when component not present.

let callBaseTypeFunc l = match l with C -> equal_c.call_base_type_func | Ada -> equal_c.call_base_type_func
let makeExpressionToStatement l = match l with C -> equal_c.makeExpressionToStatement | Ada -> equal_a.makeExpressionToStatement

let getAddres l p=
    match l with
    | Ada -> p
    | C  when p="pVal"          -> p
    | C  when p.Contains "&"    -> p
    | C                -> sprintf "(&(%s))" p


let isEqualBodyPrimitive (l:ProgrammingLanguage) (v1:FuncParamType) (v2:FuncParamType) =
    match l with
    | C         -> Some (sprintf "%s == %s" v1.p v2.p  , [])
    | Ada       -> Some (sprintf "%s = %s" v1.p v2.p   , [])

let isEqualBodyString (l:ProgrammingLanguage) (v1:FuncParamType) (v2:FuncParamType) =
    match l with
    | C         -> Some (sprintf "strcmp(%s, %s) == 0" v1.p v2.p  , [])
    | Ada       -> Some (sprintf "%s = %s" v1.p v2.p   , [])

let isEqualBodyOctetString (l:ProgrammingLanguage) sMin sMax (v1:FuncParamType) (v2:FuncParamType) =
    let v1 = sprintf "%s%s" v1.p (v1.getAcces l)
    let v2 = sprintf "%s%s" v2.p (v2.getAcces l)
    match l with
    | C         -> Some (equal_c.isEqual_OctetString v1 v2 (sMin = sMax) sMax, [])
    | Ada       -> Some (equal_a.isEqual_OctetString v1 v2 (sMin = sMax) sMax, [])

let isEqualBodyBitString (l:ProgrammingLanguage) sMin sMax (v1:FuncParamType) (v2:FuncParamType) =
    let v1 = sprintf "%s%s" v1.p (v1.getAcces l)
    let v2 = sprintf "%s%s" v2.p (v2.getAcces l)
    match l with
    | C         -> Some (equal_c.isEqual_BitString v1 v2 (sMin = sMax) sMax, [])
    | Ada       -> Some (equal_a.isEqual_BitString v1 v2 (sMin = sMax) sMax, [])

let isEqualBodySequenceOf  (childType:Asn1Type) (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.SequenceOf)  (l:ProgrammingLanguage) (v1:FuncParamType) (v2:FuncParamType)  =
    let getInnerStatement i = 
        let childAccesPath (p:FuncParamType) =  p.getArrayItem l i childType.isIA5String //v + childAccess + l.ArrName + (l.ArrayAccess i) //"[" + i + "]"
        match childType.equalFunction.isEqualBody with
        | EqualBodyExpression func  ->  
            match func (childAccesPath v1) (childAccesPath v2) with
            | Some (exp, lvars)  -> Some (sprintf "ret %s (%s);" l.AssignOperator exp, lvars)       // ret = (boolExp);
            | None      -> None
        | EqualBodyStatementList  func   -> func (childAccesPath v1) (childAccesPath v2)

    let i = sprintf "i%d" (t.id.SeqeuenceOfLevel + 1)
    let lv = SequenceOfIndex (t.id.SeqeuenceOfLevel + 1, None)
    match getInnerStatement i with
    | None when o.minSize = o.maxSize        -> None
    | None                                   ->
        match l with
        | C    -> Some (equal_c.isEqual_SequenceOf v1.p v2.p (v1.getAcces l) i (o.minSize = o.maxSize) (BigInteger o.minSize) None, lv::[])
        | Ada  -> Some (equal_a.isEqual_SequenceOf_var_size v1.p v2.p i None, lv::[])
    | Some (innerStatement, lvars)           ->
        match l with
        | C    -> Some (equal_c.isEqual_SequenceOf v1.p v2.p (v1.getAcces l) i (o.minSize = o.maxSize) (BigInteger o.minSize) (Some innerStatement), lv::lvars)
        | Ada  -> 
            match (o.minSize = o.maxSize) with
            | true  -> Some (equal_a.isEqual_SequenceOf_fix_size v1.p v2.p i  (BigInteger o.minSize) innerStatement, lv::lvars)
            | false -> Some (equal_a.isEqual_SequenceOf_var_size v1.p v2.p i  (Some innerStatement), lv::lvars)

    

let isEqualBodySequenceChild   (l:ProgrammingLanguage)  (o:Asn1AcnAst.Asn1Child) (newChild:Asn1Type) (v1:FuncParamType) (v2:FuncParamType)  = 
    let c_name = ToC o.c_name
    let sInnerStatement = 
        match newChild.equalFunction.isEqualBody with
        | EqualBodyExpression func  ->  
            match func (v1.getSeqChild l c_name newChild.isIA5String) (v2.getSeqChild l c_name newChild.isIA5String) with
            | Some (exp, lvars)  -> Some (sprintf "ret %s (%s);" l.AssignOperator exp, lvars)
            | None      -> None
        | EqualBodyStatementList  func   -> func (v1.getSeqChild l c_name newChild.isIA5String) (v2.getSeqChild l c_name newChild.isIA5String)

    match l with
    | C         -> 
        match sInnerStatement with
        | None  when  o.Optionality.IsSome  -> Some (equal_c.isEqual_Sequence_child v1.p  v2.p  (v1.getAcces l) o.Optionality.IsSome c_name None, [])
        | None                              -> None
        | Some (sInnerStatement, lvars)     -> Some (equal_c.isEqual_Sequence_child v1.p  v2.p  (v1.getAcces l) o.Optionality.IsSome c_name (Some sInnerStatement), lvars)
    | Ada       ->
        match sInnerStatement with
        | None  when  o.Optionality.IsSome  -> Some (equal_a.isEqual_Sequence_Child v1.p  v2.p  o.Optionality.IsSome c_name None, [])
        | None                              -> None
        | Some (sInnerStatement, lvars)     -> Some (equal_a.isEqual_Sequence_Child v1.p  v2.p  o.Optionality.IsSome c_name (Some sInnerStatement), lvars)


let isEqualBodySequence  (l:ProgrammingLanguage) (children:SeqChildInfo list) (v1:FuncParamType) (v2:FuncParamType)  = 
    let printChild (content:string, lvars:LocalVariable list) (sNestedContent:string option) = 
        match sNestedContent with
        | None  -> content, lvars
        | Some c-> 
            match l with
            | C        -> equal_c.JoinItems content sNestedContent, lvars
            | Ada      -> equal_a.JoinItems content sNestedContent, lvars
    let rec printChildren children : Option<string*list<LocalVariable>> = 
        match children with
        |[]     -> None
        |x::xs  -> 
            match printChildren xs with
            | None                          -> Some (printChild x  None)
            | Some (childrenCont, lvars)    -> 
                let content, lv = printChild x  (Some childrenCont)
                Some (content, lv@lvars)
        
    let childrenConent =   
        children |> 
        List.choose(fun c -> match c with Asn1Child x -> Some x | AcnChild _ -> None) |> 
        List.choose(fun c -> c.isEqualBodyStats  v1 v2 )  
    printChildren childrenConent




let isEqualBodyChoiceChild  (choiceTypeDefName:string) (l:ProgrammingLanguage) (o:Asn1AcnAst.ChChildInfo) (newChild:Asn1Type) (v1:FuncParamType) (v2:FuncParamType)  = 
    let sInnerStatement, lvars = 
        let p1,p2 =
            match l with
            | C    ->
                (v1.getChChild l o.c_name newChild.isIA5String), (v2.getChChild l o.c_name newChild.isIA5String)
            | Ada  ->
                (v1.getChChild l o.c_name newChild.isIA5String), (v2.getChChild l o.c_name newChild.isIA5String)
        match newChild.equalFunction.isEqualBody with
        | EqualBodyExpression func  ->  
            match func p1 p2 with
            | Some (exp, lvars)     -> sprintf "ret %s (%s);" l.AssignOperator exp, lvars
            | None                  -> sprintf "ret %s TRUE;" l.AssignOperator, []
        | EqualBodyStatementList  func   -> 
            match func p1 p2 with
            | Some a    -> a
            | None      -> sprintf "ret %s TRUE;" l.AssignOperator, []

    match l with
    | C         -> 
        equal_c.isEqual_Choice_Child o.presentWhenName sInnerStatement, lvars
    | Ada       ->
        equal_a.isEqual_Choice_Child o.presentWhenName sInnerStatement, lvars

let isEqualBodyChoice  (typeDefinitionCmn:TypeDefinitionCommon) (l:ProgrammingLanguage) (children:ChChildInfo list) (v1:FuncParamType) (v2:FuncParamType)  = 
    let childrenConent,lvars =   
        children |> 
        List.map(fun c -> 
            match l with
            | C    ->
                c.isEqualBodyStats v1 v2//(v1+childAccess+"u") (v2+childAccess+"u") 
            |Ada   ->
                c.isEqualBodyStats v1 v2 
        )  |>
        List.unzip
    let lvars = lvars |> List.collect id
    match l with
    |C   -> Some(equal_c.isEqual_Choice v1.p v2.p (v1.getAcces l) childrenConent, lvars)
    |Ada -> Some(equal_a.isEqual_Choice v1.p v2.p typeDefinitionCmn.name childrenConent, lvars)


let getEqualFuncName (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (id : ReferenceToType) =
    id.tasInfo |> Option.map (fun x -> ToC2(r.args.TypePrefix + x.tasName + "_Equal"))

let createNullTypeEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (o:Asn1AcnAst.NullType) =
    {
        EqualFunction.isEqualFuncName  = None
        isEqualBody                    = EqualBodyExpression (fun  v1 v2 -> None)
//        isEqualBody2                   = EqualBodyExpression2(fun  v1 v2 acc -> None)
        isEqualFunc                    = None
        isEqualFuncDef                 = None
    }    

let createEqualFunction_primitive (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (typeDefinition:TypeDefinitionCommon) isEqualBody stgMacroFunc stgMacroDefFunc  =
    let isEqualFuncName     = getEqualFuncName r l t.id
    let bodyFunc = match isEqualBody with EqualBodyExpression f -> f | EqualBodyStatementList f -> f

    let  isEqualFunc, isEqualFuncDef = 
            let p1 : FuncParamType = VALUE "val1"
            let p2 : FuncParamType = VALUE "val2"
            match isEqualFuncName with
            | None              -> None, None
            | Some funcName     -> 
                match bodyFunc p1 p2 with
                | Some (funcBody,_) -> 
                    Some (stgMacroFunc funcName typeDefinition.name funcBody), Some (stgMacroDefFunc funcName typeDefinition.name)
                | None     -> None, None

    {
        EqualFunction.isEqualFuncName  = isEqualFuncName
        isEqualBody                    = isEqualBody
//        isEqualBody2                   = 
//                    match isEqualBody with 
//                    | EqualBodyExpression f -> EqualBodyExpression2 (fun p1 p2 acc -> f p1 p2)
//                    | EqualBodyStatementList f -> EqualBodyStatementList2 (fun p1 p2 acc -> f p1 p2)
        isEqualFunc                    = isEqualFunc
        isEqualFuncDef                 = isEqualFuncDef

    }    

let stgPrintEqualPrimitive = function
    | C    -> equal_c.PrintEqualPrimitive
    | Ada  -> equal_a.PrintEqualPrimitive

let stgMacroPrimDefFunc = function
    | C    -> equal_c.PrintEqualDefintionPrimitive
    | Ada  -> equal_a.PrintEqualDefintion

let stgMacroCompDefFunc = function
    | C    -> equal_c.PrintEqualDefintionComposite
    | Ada  -> equal_a.PrintEqualDefintion

let createIntegerEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.Integer) (typeDefinition:TypeDefinitionCommon) =
    let isEqualBody         = EqualBodyExpression (isEqualBodyPrimitive l)
    createEqualFunction_primitive r l t typeDefinition isEqualBody (stgPrintEqualPrimitive l) (stgMacroPrimDefFunc l) 

let createRealEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.Real) (typeDefinition:TypeDefinitionCommon) =
    let isEqualBody         = EqualBodyExpression (isEqualBodyPrimitive l)
    createEqualFunction_primitive r l t typeDefinition isEqualBody (stgPrintEqualPrimitive l) (stgMacroPrimDefFunc l) 

let createBooleanEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.Boolean) (typeDefinition:TypeDefinitionCommon)  =
    let isEqualBody         = EqualBodyExpression (isEqualBodyPrimitive l)
    createEqualFunction_primitive r l t typeDefinition isEqualBody (stgPrintEqualPrimitive l) (stgMacroPrimDefFunc l) 

let createEnumeratedEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.Enumerated) (typeDefinition:TypeDefinitionCommon)  =
    let isEqualBody         = EqualBodyExpression (isEqualBodyPrimitive l)
    createEqualFunction_primitive r l t typeDefinition isEqualBody (stgPrintEqualPrimitive l) (stgMacroPrimDefFunc l) 

let createStringEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.StringType) (typeDefinition:TypeDefinitionCommon)  =
    let isEqualBody = EqualBodyExpression (isEqualBodyString l)
    createEqualFunction_primitive r l t typeDefinition isEqualBody (stgPrintEqualPrimitive l) (stgMacroPrimDefFunc l) 


let createOctetOrBitStringEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage)  (typeDefinition:TypeDefinitionCommon) isEqualBody stgMacroDefFunc  =
    let val1, val2 =  
        match l with 
        | C     -> POINTER "pVal1", POINTER "pVal2"
        | Ada   -> VALUE "val1", VALUE "val1"
    

    let    isEqualFuncName, isEqualFunc, isEqualFuncDef, isEqualBody                   = 
            //match baseTypeEqFunc with
            //| None     -> 
                let funcName     = typeDefinition.name + "_Equal" //getEqualFuncName r l tasInfo
                match isEqualBody val1 val2 with
                | Some (funcBody,_) -> 
                    let eqBody p1 p2 = 
                        //Some(callBaseTypeFunc l (getAddres l p1) (getAddres l p2) funcName, [])
                        isEqualBody  p1 p2
                    match l with
                    | C    -> Some funcName, Some (equal_c.PrintEqualOctBit funcName typeDefinition.name funcBody), Some (stgMacroDefFunc funcName typeDefinition.name),eqBody
                    | Ada  -> Some funcName, Some (equal_a.PrintEqualPrimitive funcName typeDefinition.name funcBody), Some (stgMacroDefFunc funcName typeDefinition.name),eqBody
                | None     -> None, None, None, isEqualBody
(*            | Some baseEqFunc              -> 
                match baseEqFunc.isEqualFuncName with
                | None  -> None, None, None, isEqualBody
                | Some eqFnc    ->
                    let eqBody acc p1 p2 = 
                        Some(callBaseTypeFunc l (getAddres l p1) (getAddres l p2) eqFnc, [])
                    None, None, None, eqBody*)
    {
        EqualFunction.isEqualFuncName  = isEqualFuncName
        isEqualBody                    = EqualBodyExpression isEqualBody 
        //isEqualBody2                   = EqualBodyExpression2 (fun p1 p2 acc ->  isEqualBody acc p1 p2)
        isEqualFunc                    = isEqualFunc
        isEqualFuncDef                 = isEqualFuncDef
    }    

let createOctetStringEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.OctetString) (typeDefinition:TypeDefinitionCommon)  =
    let isEqualBody = isEqualBodyOctetString l (BigInteger o.minSize) (BigInteger o.maxSize)
    createOctetOrBitStringEqualFunction r l  typeDefinition isEqualBody (stgMacroCompDefFunc l) 

let createBitStringEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.BitString) (typeDefinition:TypeDefinitionCommon)  =
    let isEqualBody = isEqualBodyBitString l (BigInteger o.minSize) (BigInteger o.maxSize)
    createOctetOrBitStringEqualFunction r l typeDefinition isEqualBody (stgMacroCompDefFunc l) 


let createCompositeEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage)  (typeDefinition:TypeDefinitionCommon) isEqualBody stgMacroDefFunc  =
    let val1, val2 =  
        match l with 
        | C     -> POINTER "pVal1", POINTER "pVal2"
        | Ada   -> VALUE "val1", VALUE "val1"

    let    isEqualFuncName, isEqualFunc, isEqualFuncDef, isEqualBody                   = 
            //match baseTypeEqFunc with
            //| None     -> 
                let funcName     = typeDefinition.name + "_Equal" //getEqualFuncName r l tasInfo
                match isEqualBody val1 val2 with
                | Some (funcBody,lvars) -> 
                    let eqBody (p1:FuncParamType) (p2:FuncParamType) = 
                        
                        //let exp = callBaseTypeFunc l (getAddres l p1) (getAddres l p2) funcName
                        let exp = callBaseTypeFunc l (p1.getPointer l) (p2.getPointer l) funcName
                        Some(makeExpressionToStatement l exp, [])
                    let lvars = lvars |> List.map(fun (lv:LocalVariable) -> lv.GetDeclaration l) |> Seq.distinct
                    match l with
                    | C    -> Some funcName, Some (equal_c.PrintEqualComposite funcName typeDefinition.name funcBody lvars), Some (stgMacroDefFunc funcName typeDefinition.name),eqBody
                    | Ada  -> Some funcName, Some (equal_a.PrintEqualComposite funcName typeDefinition.name funcBody lvars), Some (stgMacroDefFunc funcName typeDefinition.name),eqBody
                | None     -> None, None, None, isEqualBody
          (*  | Some baseEqFunc              -> 
                match baseEqFunc.isEqualFuncName with
                | None  -> None, None, None, isEqualBody
                | Some eqFnc    ->
                    let eqBody acc p1 p2 = 
                        let exp = callBaseTypeFunc l (getAddres l p1) (getAddres l p2) eqFnc
                        Some(makeExpressionToStatement l exp, [])
                    None, None, None, eqBody
            *)

    {
        EqualFunction.isEqualFuncName  = isEqualFuncName
        isEqualBody                    = EqualBodyStatementList (isEqualBody )
//        isEqualBody2                   = EqualBodyStatementList2(fun p1 p2 acc ->  isEqualBody acc p1 p2)
        isEqualFunc                    = isEqualFunc
        isEqualFuncDef                 = isEqualFuncDef
    }    

let createSequenceOfEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.SequenceOf) (typeDefinition:TypeDefinitionCommon) (childType:Asn1Type) =
    let isEqualBody         = isEqualBodySequenceOf childType t o l
    createCompositeEqualFunction r l  typeDefinition isEqualBody (stgMacroCompDefFunc l) 

let createSequenceEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.Sequence) (typeDefinition:TypeDefinitionCommon) (children:SeqChildInfo list)  =
    let isEqualBody         = isEqualBodySequence l children
    createCompositeEqualFunction r l  typeDefinition isEqualBody (stgMacroCompDefFunc l) 

let createChoiceEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.Choice) (typeDefinition:TypeDefinitionCommon) (children:ChChildInfo list)  =
    let isEqualBody         = isEqualBodyChoice typeDefinition l children
    createCompositeEqualFunction r l  typeDefinition isEqualBody (stgMacroCompDefFunc l) 

let createReferenceTypeEqualFunction (r:Asn1AcnAst.AstRoot) (l:ProgrammingLanguage) (t:Asn1AcnAst.Asn1Type) (o:Asn1AcnAst.ReferenceType)  (baseType:Asn1Type) =
    let typeDefinitionName = 
        //match t.tasInfo with
        //| Some tasInfo    -> ToC2(r.args.TypePrefix + tasInfo.tasName)
        //| None            -> ToC2(r.args.TypePrefix + o.tasName.Value)
        ToC2(r.args.TypePrefix + o.tasName.Value)

    let baseEqName = //typeDefinitionName + "_Equal"
        match l with
        | C     -> typeDefinitionName + "_Equal"
        | Ada   -> 
            match t.id.ModName = o.modName.Value with
            | true  -> typeDefinitionName + "_Equal"
            | false -> (ToC o.modName.Value) + "." + typeDefinitionName + "_Equal"

    match baseType.Kind with
    | Integer _
    | Real _
    | Boolean _
    | Enumerated _
    | NullType _
    | IA5String _       -> baseType.equalFunction
    | OctetString _
    | BitString  _      ->
        let    isEqualBody                   = 
            match baseType.equalFunction.isEqualFuncName with
            | None  -> (fun b c -> None)
            | Some _    ->
                let eqBody (p1:FuncParamType) (p2:FuncParamType) : (string*(LocalVariable list)) option = 
                    //let exp = callBaseTypeFunc l (getAddres l p1) (getAddres l p2) baseEqName
                    let exp = callBaseTypeFunc l (p1.getPointer l) (p2.getPointer l) baseEqName
                    Some(makeExpressionToStatement l exp, [])
                eqBody
        {
            EqualFunction.isEqualFuncName  = None
            isEqualBody                    = EqualBodyStatementList (isEqualBody )
            //isEqualBody2                   = EqualBodyStatementList2(fun p1 p2 acc ->  isEqualBody acc p1 p2)
            isEqualFunc                    = None
            isEqualFuncDef                 = None
        }    
    | SequenceOf _
    | Sequence _
    | Choice   _      ->
        let    isEqualBody                   = 
                match baseType.equalFunction.isEqualFuncName with
                | None  -> (fun b c -> None)
                | Some _    ->
                    let eqBody (p1:FuncParamType) (p2:FuncParamType) = 
                        //let exp = callBaseTypeFunc l (getAddres l p1) (getAddres l p2) baseEqName
                        let exp = callBaseTypeFunc l (p1.getPointer l) (p2.getPointer l) baseEqName
                        Some(makeExpressionToStatement l exp, [])
                    eqBody

        {
            EqualFunction.isEqualFuncName  = None
            isEqualBody                    = EqualBodyStatementList (isEqualBody )
            //isEqualBody2                   = EqualBodyStatementList2(fun p1 p2 acc ->  isEqualBody acc p1 p2)
            isEqualFunc                    = None
            isEqualFuncDef                 = None
        }    
    | ReferenceType rf ->
             baseType.equalFunction
    //| ReferenceType ref     ->
    //    ref.baseType.equalFunction