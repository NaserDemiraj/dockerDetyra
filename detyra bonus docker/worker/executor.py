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
from pathlib import Path

class CodeExecutor:
    def __init__(self):
        self.timeout = 30  # seconds
        self.max_output = 5000  # chars

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
                "execution_time": execution_time
            }
        except subprocess.TimeoutExpired:
            return {
                "success": False,
                "output": "",
                "error": "Code execution timeout (> 30 seconds)",
                "execution_time": self.timeout * 1000
            }
        except Exception as e:
            return {
                "success": False,
                "output": "",
                "error": str(e),
                "execution_time": int((time.time() - start) * 1000)
            }

    def execute_csharp(self, code):
        """Execute C# code in isolated container"""
        # For C#, we would compile and run, but for demo we'll return a placeholder
        return {
            "success": False,
            "output": "",
            "error": "C# execution not yet implemented in worker",
            "execution_time": 0
        }

    def execute(self, language, code):
        """Main execution method"""
        if language.lower() == "python":
            return self.execute_python(code)
        elif language.lower() in ["csharp", "c#"]:
            return self.execute_csharp(code)
        else:
            return {
                "success": False,
                "output": "",
                "error": f"Unsupported language: {language}",
                "execution_time": 0
            }


if __name__ == "__main__":
    # Read from stdin or environment
    language = sys.argv[1] if len(sys.argv) > 1 else "python"
    code = sys.argv[2] if len(sys.argv) > 2 else ""
    
    executor = CodeExecutor()
    result = executor.execute(language, code)
    
    print(json.dumps(result))
