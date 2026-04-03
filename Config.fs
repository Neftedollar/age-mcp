module AgeMcp.Config

open System
open System.IO
open System.Text.Json
open System.Threading.Tasks
open Npgsql
open Fyper.GraphValue

// ─── State ───

let mutable private dataSource: NpgsqlDataSource option = None
let mutable private tenantId = "default"

let configure (connStr: string) (tenant: string) =
    dataSource <- Some(NpgsqlDataSource.Create connStr)
    tenantId <- tenant

let getDataSource () =
    match dataSource with
    | Some ds -> ds
    | None -> failwith "Not configured"

let getTenantId () = tenantId

// ─── Tenant scoping ───

let scoped (name: string) = sprintf "t_%s__%s" tenantId name

let unscoped (name: string) =
    let prefix = sprintf "t_%s__" tenantId
    if name.StartsWith(prefix) then name.[prefix.Length..] else name

// ─── Connection helper (raw SQL with AGE init) ───

let withConnection (fn: NpgsqlConnection -> Task<'T>) : Task<'T> =
    task {
        let ds = getDataSource ()
        let! conn = ds.OpenConnectionAsync()
        try
            use loadCmd = new NpgsqlCommand("LOAD 'age'", conn)
            do! loadCmd.ExecuteNonQueryAsync() :> Task
            use pathCmd = new NpgsqlCommand("""SET search_path = ag_catalog, "$user", public""", conn)
            do! pathCmd.ExecuteNonQueryAsync() :> Task
            return! fn conn
        finally
            conn.Dispose()
    }

// ─── Cypher value encoding ───

let escapeStr (s: string) =
    s.Replace("\\", "\\\\").Replace("'", "\\'")

let quoteStr (s: string) = sprintf "'%s'" (escapeStr s)

let cypherValue (elem: JsonElement) =
    match elem.ValueKind with
    | JsonValueKind.String -> quoteStr (elem.GetString())
    | JsonValueKind.Number ->
        let mutable i = 0L
        if elem.TryGetInt64(&i) then string i
        else string (elem.GetDouble())
    | JsonValueKind.True -> "true"
    | JsonValueKind.False -> "false"
    | _ -> "null"

// ─── JSON helpers ───

let parseJsonObj (s: string option) =
    match s with
    | Some str when not (String.IsNullOrWhiteSpace str) ->
        use doc = JsonDocument.Parse(str)
        doc.RootElement.Clone()
    | _ ->
        use doc = JsonDocument.Parse("{}")
        doc.RootElement.Clone()

let parseJsonArr (s: string) =
    use doc = JsonDocument.Parse(s)
    doc.RootElement.Clone()

// ─── Agtype parser (varchar → GraphValue) ───

let rec private jsonElemToGv (elem: JsonElement) : GraphValue =
    match elem.ValueKind with
    | JsonValueKind.Null | JsonValueKind.Undefined -> GNull
    | JsonValueKind.True -> GBool true
    | JsonValueKind.False -> GBool false
    | JsonValueKind.Number ->
        let mutable i = 0L
        if elem.TryGetInt64(&i) then GInt i else GFloat(elem.GetDouble())
    | JsonValueKind.String -> GString(elem.GetString())
    | JsonValueKind.Array ->
        elem.EnumerateArray() |> Seq.map jsonElemToGv |> Seq.toList |> GList
    | JsonValueKind.Object ->
        elem.EnumerateObject() |> Seq.map (fun p -> p.Name, jsonElemToGv p.Value) |> Map.ofSeq |> GMap
    | _ -> GNull

let private parseVertex (json: string) : GraphValue =
    try
        use doc = JsonDocument.Parse(json)
        let r = doc.RootElement
        let id = if r.TryGetProperty("id") |> fst then r.GetProperty("id").GetInt64() else 0L
        let label = if r.TryGetProperty("label") |> fst then r.GetProperty("label").GetString() else ""
        let props =
            if r.TryGetProperty("properties") |> fst then
                r.GetProperty("properties").EnumerateObject()
                |> Seq.map (fun p -> p.Name, jsonElemToGv p.Value)
                |> Map.ofSeq
            else Map.empty
        GNode { Id = id; Labels = [ label ]; Properties = props }
    with _ -> GString json

let private parseEdge (json: string) : GraphValue =
    try
        use doc = JsonDocument.Parse(json)
        let r = doc.RootElement
        let id = if r.TryGetProperty("id") |> fst then r.GetProperty("id").GetInt64() else 0L
        let label = if r.TryGetProperty("label") |> fst then r.GetProperty("label").GetString() else ""
        let startId = if r.TryGetProperty("start_id") |> fst then r.GetProperty("start_id").GetInt64() else 0L
        let endId = if r.TryGetProperty("end_id") |> fst then r.GetProperty("end_id").GetInt64() else 0L
        let props =
            if r.TryGetProperty("properties") |> fst then
                r.GetProperty("properties").EnumerateObject()
                |> Seq.map (fun p -> p.Name, jsonElemToGv p.Value)
                |> Map.ofSeq
            else Map.empty
        GRel { Id = id; RelType = label; StartNodeId = startId; EndNodeId = endId; Properties = props }
    with _ -> GString json

let private parseScalar (value: string) : GraphValue =
    if value = "true" then GBool true
    elif value = "false" then GBool false
    elif value = "null" || String.IsNullOrEmpty value then GNull
    elif value.StartsWith("\"") && value.EndsWith("\"") then
        GString(value.[1..value.Length - 2])
    elif value.StartsWith("[") then
        try use doc = JsonDocument.Parse(value)
            doc.RootElement.EnumerateArray() |> Seq.map jsonElemToGv |> Seq.toList |> GList
        with _ -> GString value
    elif value.StartsWith("{") then
        try use doc = JsonDocument.Parse(value)
            doc.RootElement.EnumerateObject() |> Seq.map (fun p -> p.Name, jsonElemToGv p.Value) |> Map.ofSeq |> GMap
        with _ -> GString value
    else
        match Int64.TryParse(value) with
        | true, i -> GInt i
        | _ ->
            match Double.TryParse(value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
            | true, f -> GFloat f
            | _ -> GString value

let parseAgtype (raw: string) : GraphValue =
    if String.IsNullOrWhiteSpace raw then GNull
    else
        let t = raw.Trim()
        if t.EndsWith("::vertex") then parseVertex(t.[.. t.Length - 9])
        elif t.EndsWith("::edge") then parseEdge(t.[.. t.Length - 7])
        elif t.EndsWith("::path") then GString t  // paths returned as-is
        else parseScalar t

// ─── Return alias extraction ───

let extractReturnAliases (cypher: string) : string list =
    let idx = cypher.IndexOf("RETURN", StringComparison.OrdinalIgnoreCase)
    if idx < 0 then [ "result" ]
    else
        let after = cypher.[idx + 6..].Trim()
        let cleaned =
            if after.StartsWith("DISTINCT", StringComparison.OrdinalIgnoreCase)
            then after.[8..].Trim()
            else after
        let endIdx =
            [ " ORDER "; " LIMIT "; " SKIP " ]
            |> List.choose (fun kw ->
                let i = cleaned.IndexOf(kw, StringComparison.OrdinalIgnoreCase)
                if i >= 0 then Some i else None)
            |> function [] -> cleaned.Length | xs -> List.min xs
        let returnPart = cleaned.[.. endIdx - 1].Trim()
        returnPart.Split(',')
        |> Array.mapi (fun i part ->
            let p = part.Trim()
            let asIdx = p.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase)
            if asIdx >= 0 then p.[asIdx + 4..].Trim()
            elif p.Contains(".") || p.Contains("(") then sprintf "col%d" i
            else p)
        |> Array.toList

// ─── Cypher execution (bypasses Fyper.Age, uses ::varchar cast) ───

let private executeCypherOnConn
    (conn: NpgsqlConnection)
    (tx: NpgsqlTransaction option)
    (scopedName: string)
    (cypher: string)
    : Task<GraphRecord list> =
    task {
        let aliases = extractReturnAliases cypher
        let innerCols = aliases |> List.map (fun a -> sprintf "%s agtype" a) |> String.concat ", "
        let selectCols = aliases |> List.map (fun a -> sprintf "%s::varchar" a) |> String.concat ", "
        let sql = sprintf "SELECT %s FROM cypher('%s', $$ %s $$) AS (%s)" selectCols (escapeStr scopedName) cypher innerCols
        use cmd =
            match tx with
            | Some t -> new NpgsqlCommand(sql, conn, t)
            | None -> new NpgsqlCommand(sql, conn)
        use! reader = cmd.ExecuteReaderAsync()
        let records = ResizeArray<GraphRecord>()
        while reader.Read() do
            let values =
                aliases
                |> List.mapi (fun i alias ->
                    let raw = if reader.IsDBNull(i) then "" else reader.GetString(i)
                    alias, parseAgtype raw)
                |> Map.ofList
            records.Add({ Keys = aliases; Values = values })
        return records |> Seq.toList
    }

let executeCypherRead (graphName: string) (cypher: string) : Task<GraphRecord list> =
    withConnection (fun conn -> executeCypherOnConn conn None (scoped graphName) cypher)

let executeCypherWrite (graphName: string) (cypher: string) : Task<int> =
    withConnection (fun conn -> task {
        let! records = executeCypherOnConn conn None (scoped graphName) cypher
        return records.Length
    })

/// Execute multiple cypher reads on a single connection (saves LOAD 'age' + SET per query)
let executeCypherBatch (graphName: string) (queries: string list) : Task<GraphRecord list list> =
    withConnection (fun conn -> task {
        let sn = scoped graphName
        let results = ResizeArray<GraphRecord list>()
        for q in queries do
            let! records = executeCypherOnConn conn None sn q
            results.Add(records)
        return results |> Seq.toList
    })

// ─── Transaction helper ───

type CypherTx = {
    Read: string -> Task<GraphRecord list>
    Write: string -> Task<int>
}

let withCypherTransaction (graphName: string) (fn: CypherTx -> Task<'T>) : Task<'T> =
    task {
        let ds = getDataSource ()
        let! conn = ds.OpenConnectionAsync()
        try
            use loadCmd = new NpgsqlCommand("LOAD 'age'", conn)
            do! loadCmd.ExecuteNonQueryAsync() :> Task
            use pathCmd = new NpgsqlCommand("""SET search_path = ag_catalog, "$user", public""", conn)
            do! pathCmd.ExecuteNonQueryAsync() :> Task
            let! tx = conn.BeginTransactionAsync()
            let sn = scoped graphName
            let executor = {
                Read = fun cypher -> executeCypherOnConn conn (Some tx) sn cypher
                Write = fun cypher -> task {
                    let! records = executeCypherOnConn conn (Some tx) sn cypher
                    return records.Length
                }
            }
            try
                let! result = fn executor
                do! tx.CommitAsync()
                return result
            with ex ->
                try do! tx.RollbackAsync() with _ -> ()
                raise ex
                return Unchecked.defaultof<'T> // unreachable
        finally
            conn.Dispose()
    }

// ─── TTL cache for metadata queries ───

open System.Collections.Concurrent

type private CacheEntry<'T> = { Value: 'T; ExpiresAt: DateTime }

let private cache = ConcurrentDictionary<string, obj>()

let withTtlCache (key: string) (ttl: TimeSpan) (fn: unit -> Task<'T>) : Task<'T> =
    task {
        match cache.TryGetValue(key) with
        | true, entry ->
            let e = entry :?> CacheEntry<'T>
            if DateTime.UtcNow < e.ExpiresAt then return e.Value
            else
                let! value = fn ()
                cache.[key] <- { Value = value; ExpiresAt = DateTime.UtcNow + ttl } :> obj
                return value
        | _ ->
            let! value = fn ()
            cache.[key] <- { Value = value; ExpiresAt = DateTime.UtcNow + ttl } :> obj
            return value
    }

let invalidateCache (prefix: string) =
    for key in cache.Keys do
        if key.StartsWith(prefix) then cache.TryRemove(key) |> ignore

// ─── GraphValue → JSON serialization ───

let rec writeGv (w: Utf8JsonWriter) (v: GraphValue) =
    match v with
    | GNull -> w.WriteNullValue()
    | GBool b -> w.WriteBooleanValue(b)
    | GInt i -> w.WriteNumberValue(i)
    | GFloat f -> w.WriteNumberValue(f)
    | GString s -> w.WriteStringValue(s)
    | GList items ->
        w.WriteStartArray()
        for item in items do writeGv w item
        w.WriteEndArray()
    | GMap m ->
        w.WriteStartObject()
        for kv in m do
            w.WritePropertyName(kv.Key)
            writeGv w kv.Value
        w.WriteEndObject()
    | GNode n ->
        w.WriteStartObject()
        w.WriteNumber("id", n.Id)
        w.WriteString("label", n.Labels |> List.tryHead |> Option.defaultValue "")
        for kv in n.Properties do
            w.WritePropertyName(kv.Key)
            writeGv w kv.Value
        w.WriteEndObject()
    | GRel r ->
        w.WriteStartObject()
        w.WriteNumber("id", r.Id)
        w.WriteString("label", r.RelType)
        w.WriteNumber("start_id", r.StartNodeId)
        w.WriteNumber("end_id", r.EndNodeId)
        for kv in r.Properties do
            w.WritePropertyName(kv.Key)
            writeGv w kv.Value
        w.WriteEndObject()
    | GPath p ->
        w.WriteStartObject()
        w.WritePropertyName("nodes")
        w.WriteStartArray()
        for n in p.Nodes do writeGv w (GNode n)
        w.WriteEndArray()
        w.WritePropertyName("relationships")
        w.WriteStartArray()
        for r in p.Relationships do writeGv w (GRel r)
        w.WriteEndArray()
        w.WriteEndObject()

let toJson (fn: Utf8JsonWriter -> unit) =
    use ms = new MemoryStream()
    let w = new Utf8JsonWriter(ms, JsonWriterOptions(Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping))
    fn w
    w.Flush()
    let result = Text.Encoding.UTF8.GetString(ms.ToArray())
    w.Dispose()
    result

// ─── GraphValue helpers ───

let gvToStr (v: GraphValue) =
    match v with
    | GString s -> s
    | GInt i -> string i
    | GFloat f -> string f
    | GBool b -> string b
    | _ -> ""

let escapeJson (s: string) =
    s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "")

let recordsToJson (records: GraphRecord list) =
    toJson (fun w ->
        w.WriteStartArray()
        for r in records do
            if r.Values.Count = 1 then
                let (_, v) = r.Values |> Map.toList |> List.head
                writeGv w v
            else
                w.WriteStartObject()
                for kv in r.Values do
                    w.WritePropertyName(kv.Key)
                    writeGv w kv.Value
                w.WriteEndObject()
        w.WriteEndArray())
