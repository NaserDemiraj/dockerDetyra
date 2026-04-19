#!/usr/bin/env python3
"""
Worker Container - Isolated Code Execution
Runs inside Docker and executes student code with resource limits
"""

import sys
import json
import subprocess
import time
import shutil
import os
import re
import tempfile

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
            error = None
            error_type = None
            if result.returncode != 0:
                error_type, error = self._categorize_python_error(result.returncode, result.stderr)

            return {
                "success": result.returncode == 0,
                "output": output,
                "error": error,
                "error_type": error_type,
                "execution_time": execution_time,
                "language": "python"
            }
        except subprocess.TimeoutExpired:
            return {
                "success": False,
                "output": "",
                "error": "⏱️ TIMEOUT: Code execution exceeded 30 seconds limit. Infinite loop detected?",
                "error_type": "timeout",
                "execution_time": self.timeout * 1000,
                "language": "python"
            }
        except Exception as e:
            return {
                "success": False,
                "output": "",
                "error": f"Python Error: {str(e)}",
                "error_type": "runtime_exception",
                "execution_time": int((time.time() - start) * 1000),
                "language": "python"
            }

    def execute_csharp(self, code):
        """Execute C# code using Mono (mcs compiler + mono runtime)"""
        start = time.time()
        temp_dir = tempfile.mkdtemp()
        try:
            # Wrap bare statements in a class/Main if the code doesn't define its own
            if "class " not in code and "static void Main" not in code and "static async" not in code:
                csharp_program = (
                    "using System;\n"
                    "using System.Collections.Generic;\n"
                    "using System.Linq;\n"
                    "using System.Text;\n"
                    "class Program {\n"
                    "    static void Main(string[] args) {\n"
                    f"        {code}\n"
                    "    }\n"
                    "}\n"
                )
            else:
                csharp_program = code

            cs_file = os.path.join(temp_dir, "program.cs")
            exe_file = os.path.join(temp_dir, "program.exe")

            with open(cs_file, "w") as f:
                f.write(csharp_program)

            # Compile with mcs
            compile_result = subprocess.run(
                ["mcs", f"-out:{exe_file}", cs_file],
                capture_output=True,
                text=True,
                timeout=30,
                cwd=temp_dir
            )

            if compile_result.returncode != 0:
                return {
                    "success": False,
                    "output": "",
                    "error": "Compile Error:\n" + compile_result.stderr,
                    "error_type": "compilation_error",
                    "execution_time": int((time.time() - start) * 1000),
                    "language": "csharp"
                }

            # Run with mono
            run_result = subprocess.run(
                ["mono", exe_file],
                capture_output=True,
                text=True,
                timeout=self.timeout,
                cwd=temp_dir
            )

            execution_time = int((time.time() - start) * 1000)
            output = (run_result.stdout + run_result.stderr)[:self.max_output]
            error = None
            error_type = None
            if run_result.returncode != 0:
                error = run_result.stderr or "Runtime error"
                error_type = "runtime_exception"

            return {
                "success": run_result.returncode == 0,
                "output": output,
                "error": error,
                "error_type": error_type,
                "execution_time": execution_time,
                "language": "csharp"
            }
        except subprocess.TimeoutExpired:
            return {
                "success": False,
                "output": "",
                "error": "⏱️ TIMEOUT: C# code execution exceeded 30 seconds limit",
                "error_type": "timeout",
                "execution_time": self.timeout * 1000,
                "language": "csharp"
            }
        except Exception as e:
            return {
                "success": False,
                "output": "",
                "error": f"C# Error: {str(e)}",
                "error_type": "runtime_exception",
                "execution_time": int((time.time() - start) * 1000),
                "language": "csharp"
            }
        finally:
            shutil.rmtree(temp_dir, ignore_errors=True)

    def _categorize_python_error(self, return_code, stderr_text):
        error_text = (stderr_text or "").strip()
        normalized = error_text.lower()

        if return_code in (137, -9) or "memoryerror" in normalized or "out of memory" in normalized:
            return "memory_limit_violation", "Memory Limit Exceeded: Code exceeded the 512MB memory limit."

        if "syntaxerror" in normalized or "indentationerror" in normalized:
            line_match = re.search(r"line (\d+)", error_text)
            line_info = f" on line {line_match.group(1)}" if line_match else ""
            last_line = error_text.splitlines()[-1] if error_text else "SyntaxError"
            return "compilation_error", f"Compilation Error{line_info}: {last_line}"

        if "traceback" in normalized:
            line_match = re.search(r'File "<string>", line (\d+)', error_text)
            line_info = f" on line {line_match.group(1)}" if line_match else ""
            last_line = error_text.splitlines()[-1] if error_text else "Runtime exception"
            return "runtime_exception", f"Runtime Exception{line_info}: {last_line}"

        return "runtime_exception", error_text or "Runtime Exception"

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
                "error_type": "unsupported_language",
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
            "error_type": "executor_error",
            "execution_time": 0
        }))
