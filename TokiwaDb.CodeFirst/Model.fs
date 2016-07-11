namespace TokiwaDb.CodeFirst.Detail

open System
open System.Reflection
open TokiwaDb.Core
open TokiwaDb.CodeFirst

module Value =
  let toObj =
    function
    | Int x           -> x :> obj
    | Float x         -> x :> obj
    | String x        -> x :> obj
    | Binary x        -> x :> obj
    | Time x          -> x :> obj

  let ofObj (x: obj) =
    match x with
    | :? int64        as x -> Int x
    | :? float        as x -> Float x
    | :? string       as x -> Value.String x
    | :? array<byte>  as x -> Binary x
    | :? DateTime     as x -> Time x
    | _ -> raise (Exception())

module Model =
  let hasId (m: #IModel) =
    m.Id >= 0L

  /// Properties which fields of a record maps to.
  let mappedProperties (modelType: Type) =
    let bindingFlags =
          BindingFlags.DeclaredOnly
      ||| BindingFlags.Instance
      ||| BindingFlags.Public
    in
      modelType.GetProperties(bindingFlags)
      |> Array.filter (fun pi -> pi.SetMethod <> null)

  /// Get an array of Field, excluding "id".
  let toFields =
    let map =
      [
        (typeof<int64>          , Field.int)
        (typeof<float>          , Field.float)
        (typeof<string>         , Field.string)
        (typeof<DateTime>       , Field.time)
      ]
      |> dict
    fun (modelType: Type) ->
      mappedProperties modelType
      |> Array.map (fun pi ->
        let f =
          match map.TryGetValue(pi.PropertyType) with
          | (true, f) -> f
          | (false, _) -> Field.binary
        in f pi.Name
        )

  /// Create a record, excluding "id".
  let toRecord modelType (model: IModel) =
    mappedProperties modelType
    |> Array.map (fun pi -> pi.GetValue(model) |> Value.ofObj)

  /// Create an instance of model from a mortal record with "id".
  let ofMortalRecord (modelType: Type) (record: MortalValue<Record>): IModel =
    let m     = Activator.CreateInstance(modelType) :?> IModel
    let set propertyName value =
      modelType.GetProperty(propertyName).SetValue(m, value)
    let ()    =
      m.Id <- record.Value |> Record.tryId |> Option.get
      m.Birth <- record.Birth
      m.Death <- record.Death
    let ()    =
      Array.zip
        (record.Value |> Record.dropId)
        (mappedProperties modelType)
      |> Array.iter (fun (value, pi) -> pi.SetValue(m, value |> Value.toObj))
    in m
