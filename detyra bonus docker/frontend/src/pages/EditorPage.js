import React, { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { codeApi } from '../services/api';
import '../styles/EditorPage.css';

function EditorPage({ onLogout }) {
  const [language, setLanguage] = useState('python');
  const [code, setCode] = useState('# Write your code here\nprint("Hello, World!")');
  const [output, setOutput] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [executionTime, setExecutionTime] = useState(0);
  const navigate = useNavigate();
  const textareaRef = useRef(null);

  const handleLanguageChange = (e) => {
    const newLang = e.target.value;
    setLanguage(newLang);
    
    // Set initial code for language
    if (newLang === 'python') {
      setCode('# Write your Python code here\nprint("Hello, World!")');
    } else {
      setCode('// Write your C# code here\nConsole.WriteLine("Hello, World!");');
    }
  };

  const handleExecute = async () => {
    setError('');
    setOutput('');
    setExecutionTime(0);
    setLoading(true);

    try {
      const response = await codeApi.execute(language, code);
      const data = response.data;
      
      setExecutionTime(data.executionTimeMs || 0);
      
      if (data.success) {
        setOutput(data.output || '(No output)');
      } else {
        setError(data.error || 'Execution failed');
      }
    } catch (err) {
      const errorMsg = err.response?.data?.error || err.message || 'Failed to execute code';
      setError(errorMsg);
      console.error('Execution error:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    onLogout();
    navigate('/login');
  };

  const handleCodeChange = (e) => {
    setCode(e.target.value);
  };

  return (
    <div className="editor-container">
      <div className="navbar">
        <h1>💻 CodeLab Editor</h1>
        <button className="logout-btn" onClick={handleLogout}>Logout</button>
      </div>

      <div className="editor-main">
        <div className="editor-panel">
          <div className="editor-header">
            <div className="language-section">
              <label>Language:</label>
              <select value={language} onChange={handleLanguageChange} className="language-select">
                <option value="python">🐍 Python</option>
                <option value="csharp">C# / C#</option>
              </select>
            </div>
            <button 
              className="execute-btn" 
              onClick={handleExecute} 
              disabled={loading}
            >
              {loading ? '⏳ Executing...' : '▶️ Execute'}
            </button>
          </div>
          <textarea 
            ref={textareaRef}
            className="code-editor"
            value={code} 
            onChange={handleCodeChange}
            placeholder="Write your code here..."
            spellCheck="false"
          />
        </div>

        <div className="output-panel">
          <h3>📊 Output & Results</h3>
          {error && (
            <div className="error-box">
              <strong>❌ Error:</strong>
              <pre>{error}</pre>
            </div>
          )}
          {output && (
            <div className="output-box">
              <pre>{output}</pre>
              {executionTime > 0 && (
                <div className="execution-time">⏱️ Execution time: {executionTime}ms</div>
              )}
            </div>
          )}
          {!error && !output && !loading && (
            <p className="placeholder">👉 Write code and click Execute to see results...</p>
          )}
          {loading && (
            <p className="placeholder">⌛ Executing your code...</p>
          )}
        </div>
      </div>
    </div>
  );
}

export default EditorPage;
