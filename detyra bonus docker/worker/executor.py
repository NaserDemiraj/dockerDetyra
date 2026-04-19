#!/usr/bin/env python3
"""
Worker Container - Isolated Code Execution
Runs inside Docker and executes student code with resource limits
"""

import sys
import json
import subprocess
import time
import signal
import os
from pathlib import Path

class CodeExecutor:
    def __init__(self):
        self.timeout = 30  # seconds
        self.max_output = 10000  # chars

    def execute_python(self, code):
        """Execute Python code safely"""
        start = time.time()
        try:
            result = subprocess.run(
                [sys.executable, "-c", code],
                capture_output=True,
                text=True,
                timeout=self.timeout,
                cwd="/tmp"
            )
            execution_time = int((time.time() - start) * 1000)
            
            output = (result.stdout + result.stderr)[:self.max_output]
            return {
                "success": result.returncode == 0,
                "output": output,
                "error": result.stderr if result.returncode != 0 else None,
                "execution_time": execution_time,
                "language": "python"
            }
        except subprocess.TimeoutExpired:
            return {
                "success": False,
                "output": "",
                "error": "⏱️ TIMEOUT: Code execution exceeded 30 seconds limit. Infinite loop detected?",
                "execution_time": self.timeout * 1000,
                "language": "python"
            }
        except Exception as e:
            return {
                "success": False,
                "output": "",
                "error": f"Python Error: {str(e)}",
                "execution_time": int((time.time() - start) * 1000),
                "language": "python"
            }

    def execute_csharp(self, code):
        """Execute C# code - simplified for demo"""
        start = time.time()
        try:
            # Create a minimal C# program
            csharp_program = f"""
using System;
class Program {{
    static void Main() {{
        {code}
    }}
}}
"""
            # Create temp file
            temp_file = "/tmp/temp_program.cs"
            with open(temp_file, "w") as f:
                f.write(csharp_program)
            
            # Compile (requires mono or dotnet to be installed in container)
            # For now, return a simulated result showing it would work
            return {
                "success": True,
                "output": "C# execution would compile and run the code\n(Full .NET runtime needed in production)",
                "error": None,
                "execution_time": int((time.time() - start) * 1000),
                "language": "csharp"
            }
        except subprocess.TimeoutExpired:
            return {
                "success": False,
                "output": "",
                "error": "⏱️ TIMEOUT: C# code execution exceeded 30 seconds limit",
                "execution_time": self.timeout * 1000,
                "language": "csharp"
            }
        except Exception as e:
            return {
                "success": False,
                "output": "",
                "error": f"C# Compilation Error: {str(e)}",
                "execution_time": int((time.time() - start) * 1000),
                "language": "csharp"
            }

    def execute(self, language, code):
        """Main execution method"""
        if language.lower() == "python":
            return self.execute_python(code)
        elif language.lower() in ["csharp", "c#", "cs"]:
            return self.execute_csharp(code)
        else:
            return {
                "success": False,
                "output": "",
                "error": f"❌ Unsupported language: {language}. Supported: python, csharp",
                "execution_time": 0
            }


if __name__ == "__main__":
    try:
        # Read arguments: executor.py <language> <code_file_path>
        if len(sys.argv) < 3:
            print(json.dumps({
                "success": False,
                "output": "",
                "error": "Invalid arguments. Usage: executor.py <language> <code_file_path>",
                "execution_time": 0
            }))
            sys.exit(1)
        
        language = sys.argv[1]
        code_file = sys.argv[2]
        
        # Read code from file
        with open(code_file, "r") as f:
            code = f.read()
        
        executor = CodeExecutor()
        result = executor.execute(language, code)
        
        print(json.dumps(result))
    except Exception as e:
        print(json.dumps({
            "success": False,
            "output": "",
            "error": f"Executor Error: {str(e)}",
            "execution_time": 0
        }))
