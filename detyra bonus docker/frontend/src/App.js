import React, { useState, useEffect } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import EditorPage from './pages/EditorPage';
import { authApi } from './services/api';
import './App.css';

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(!!localStorage.getItem('token'));
  const [isAutoLoggingIn, setIsAutoLoggingIn] = useState(true);

  useEffect(() => {
    const autoLogin = async () => {
      const token = localStorage.getItem('token');
      if (!token) {
        try {
          const response = await authApi.login('testuser', 'Test@123456');
          if (response.data.success) {
            localStorage.setItem('token', response.data.token);
            setIsAuthenticated(true);
          }
        } catch (err) {
          console.error('Auto-login failed:', err);
        }
      }
      setIsAutoLoggingIn(false);
    };

    autoLogin();
  }, []);

  const handleLogin = (token) => {
    localStorage.setItem('token', token);
    setIsAuthenticated(true);
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    setIsAuthenticated(false);
  };

  if (isAutoLoggingIn) {
    return <div className="auth-container"><div className="auth-card"><h1>Loading...</h1></div></div>;
  }

  return (
    <Router>
      <Routes>
        <Route path="/login" element={<LoginPage onLogin={handleLogin} />} />
        <Route path="/register" element={<RegisterPage onRegister={handleLogin} />} />
        <Route 
          path="/editor" 
          element={isAuthenticated ? <EditorPage onLogout={handleLogout} /> : <Navigate to="/login" />} 
        />
        <Route path="/" element={<Navigate to={isAuthenticated ? "/editor" : "/login"} />} />
      </Routes>
    </Router>
  );
}

export default App;
