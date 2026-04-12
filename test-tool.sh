#!/bin/bash
# End-to-end test of age-mcp dotnet tool via MCP JSON-RPC over stdio

export AGE_CONNECTION_STRING="Host=localhost;Port=5435;Database=agemcp;Username=agemcp;Password=agemcp"
export TENANT_ID="default"
export PATH="$PATH:$HOME/.dotnet/tools"

# MCP messages
INIT='{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
INITIALIZED='{"jsonrpc":"2.0","method":"notifications/initialized"}'
LIST_TOOLS='{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
CALL_LIST='{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"list_graphs","arguments":{"dummy":null}}}'
CALL_SEARCH='{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"search_vertices","arguments":{"graph_name":"people","label":"Person","limit":2}}}'

# Send all messages via stdin, collect stdout
{
  echo "$INIT"
  sleep 0.5
  echo "$INITIALIZED"
  echo "$LIST_TOOLS"
  echo "$CALL_LIST"
  echo "$CALL_SEARCH"
  sleep 2
} | age-mcp 2>/dev/null | while IFS= read -r line; do
  echo "$line" | python3 -c "
import sys, json
try:
    msg = json.loads(sys.stdin.read())
    mid = msg.get('id', '?')
    if mid == 1:
        tools = [t['name'] for t in msg.get('result',{}).get('capabilities',{}).get('tools',{}).get('listChanged', [])]
        print(f'[init] OK — protocol {msg[\"result\"][\"protocolVersion\"]}')
    elif mid == 2:
        tools = [t['name'] for t in msg.get('result',{}).get('tools',[])]
        print(f'[tools/list] {len(tools)} tools: {tools[:5]}...')
    elif mid == 3:
        content = msg.get('result',{}).get('content',[{}])[0].get('text','')
        print(f'[list_graphs] {content[:120]}')
    elif mid == 4:
        content = msg.get('result',{}).get('content',[{}])[0].get('text','')
        print(f'[search_vertices] {content[:120]}')
    else:
        print(f'[msg {mid}] {json.dumps(msg)[:120]}')
except Exception as e:
    print(f'[raw] {sys.stdin.read()[:120]}')
" 2>/dev/null
done
