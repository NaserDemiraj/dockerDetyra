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
  const navigate = useNavigate();
  const editorRef = useRef(null);

  useEffect(() => {
    if (window.CodeMirror) {
      editorRef.current = window.CodeMirror.fromTextArea(
        document.getElementById('code-editor'),
        {
          lineNumbers: true,
          mode: language === 'python' ? 'python' : 'text/x-csharp',
          theme: 'default',
          fontSize: '14px',
        }
      );
      editorRef.current.setValue(code);
      editorRef.current.on('change', () => setCode(editorRef.current.getValue()));
    }
  }, []);

  const handleLanguageChange = (e) => {
    const newLang = e.target.value;
    setLanguage(newLang);
    if (editorRef.current) {
      editorRef.current.setOption('mode', newLang === 'python' ? 'python' : 'text/x-csharp');
    }
  };

  const handleExecute = async () => {
    setError('');
    setOutput('');
    setLoading(true);

    try {
      const response = await codeApi.execute(language, code);
      if (response.data.success) {
        setOutput(response.data.output);
      } else {
        setError(response.data.error || 'Execution failed');
      }
    } catch (err) {
      setError(err.response?.data?.error || 'Failed to execute code');
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    onLogout();
    navigate('/login');
  };

  return (
    <div className="editor-container">
      <div className="navbar">
        <h1>CodeLab Editor</h1>
        <button className="logout-btn" onClick={handleLogout}>Logout</button>
      </div>

      <div className="editor-main">
        <div className="editor-panel">
          <div className="editor-header">
            <select value={language} onChange={handleLanguageChange} className="language-select">
              <option value="python">Python</option>
              <option value="csharp">C#</option>
            </select>
            <button className="execute-btn" onClick={handleExecute} disabled={loading}>
              {loading ? 'Executing...' : 'Execute'}
            </button>
          </div>
          <textarea id="code-editor" value={code} onChange={(e) => setCode(e.target.value)} />
        </div>

        <div className="output-panel">
          <h3>Output</h3>
          {error && (
            <div className="error-box">
              <strong>Error:</strong>
              <pre>{error}</pre>
            </div>
          )}
          {output && (
            <div className="output-box">
              <pre>{output}</pre>
            </div>
          )}
          {!error && !output && <p className="placeholder">Output will appear here...</p>}
        </div>
      </div>
    </div>
  );
}

export default EditorPage;
