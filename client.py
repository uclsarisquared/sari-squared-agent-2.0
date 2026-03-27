import asyncio
import sys
import time
import os
from langchain_mcp_adapters.client import MultiServerMCPClient
from langgraph.graph import StateGraph, MessagesState, START
from langgraph.prebuilt import ToolNode, tools_condition
from langchain.chat_models import init_chat_model

model_base_url = os.environ["UCL_MODEL_BASE_URL"]

# --- Setup ---
model = init_chat_model(
    "openai:Qwen/Qwen3.5-27B",
    base_url=model_base_url+":8000/v1",
    api_key="secret"
)

client = MultiServerMCPClient({
    "sari-mcp": {
        "command": 'C:\\Sari\\sari_env\\python.exe',
        "args": ['mcp_server.py'],
        "transport": "stdio",
    }
})

def typewriter_print(text, delay=0.02):
    """Prints text character by character."""
    for char in text:
        sys.stdout.write(char)
        sys.stdout.flush()
        time.sleep(delay)
    # Ensure a newline at the end


async def get_agent():
    tools = await client.get_tools()

    def call_model(state: MessagesState):
        # We bind tools here so the model knows it can use them
        return {"messages": [model.bind_tools(tools).invoke(state["messages"])]}

    builder = StateGraph(MessagesState)
    builder.add_node("call_model", call_model)
    builder.add_node("tools", ToolNode(tools))
    builder.add_edge(START, "call_model")
    builder.add_conditional_edges("call_model", tools_condition)
    builder.add_edge("tools", "call_model")

    return builder.compile()

async def chat_loop():
    agent = await get_agent()
    # Keep track of the conversation history
    thread_state = {"messages": []}

    print("--- MCP Chat Agent (Type 'exit' to quit) ---")

    while True:
        user_input = input("\nYou: ")
        if user_input.lower() in ["exit", "quit", "q"]:
            break

        thread_state["messages"].append({"role": "user", "content": user_input})
        # time the agent's response for performance monitoring
        start_time = time.perf_counter()
        print("Agent: ", end="", flush=True)
        
        # We use astream to get updates. 
        # 'stream_mode="values"' gives us the full state after each step.
        last_message_content = ""
        async for event in agent.astream(thread_state, stream_mode="values"):
            # Get the very last message in the current state
            last_msg = event["messages"][-1]
            
            # We only want to print if the model actually spoke (AssistantMessage)
            # and avoid re-printing if the agent is just calling tools.
            if last_msg.type == "ai" and last_msg.content:
                # In a simple loop, the last event will contain the full final response.
                last_message_content = last_msg.content
        end_time = time.perf_counter()
        duratinon = end_time - start_time

        # Apply the typewriter effect to the final response
        typewriter_print(last_message_content)
        print() # New line
        print(f"(Response generated in {duratinon:.2f} seconds)")
        
        # Update our history so the agent remembers the context
        thread_state["messages"].append({"role": "assistant", "content": last_message_content})

if __name__ == "__main__":
    try:
        asyncio.run(chat_loop())
    except KeyboardInterrupt:
        pass