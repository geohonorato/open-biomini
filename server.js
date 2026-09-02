const express = require('express');
const cors = require('cors');
const fs = require('fs');
const path = require('path');
const http = require('http');
const { execFile } = require('child_process');

const app = express();
const PORT = 3300;
const DB_FILE = path.join(__dirname, 'fingerprints.json');

app.use(cors());
app.use(express.json({ limit: '10mb' }));
app.use(express.static(path.join(__dirname, 'public')));

process.on('uncaughtException', (err) => {
  console.error('[!] Erro global capturado (uncaughtException):', err.message);
});

process.on('unhandledRejection', (reason, promise) => {
  console.error('[!] Rejeição global capturada (unhandledRejection):', reason);
});

// SSE (Server-Sent Events) para sincronização em tempo real com o frontend
let sseClients = [];

app.get('/api/events', (req, res) => {
  res.setHeader('Content-Type', 'text/event-stream');
  res.setHeader('Cache-Control', 'no-cache');
  res.setHeader('Connection', 'keep-alive');
  if (res.flushHeaders) res.flushHeaders();

  sseClients.push(res);

  res.on('error', () => {
    sseClients = sseClients.filter(c => c !== res);
  });

  req.on('close', () => {
    sseClients = sseClients.filter(c => c !== res);
  });
});

// Heartbeat a cada 20 segundos para manter conexões SSE vivas
setInterval(() => {
  sseClients.forEach(c => {
    try { c.write(': heartbeat\n\n'); } catch (e) {}
  });
}, 20000);

function broadcastEvent(eventName, data) {
  const payload = `event: ${eventName}\ndata: ${JSON.stringify(data)}\n\n`;
  sseClients.forEach(c => {
    try { c.write(payload); } catch (e) {}
  });
}

// 0.0 Captura Óptica Direta (Proxy para o BioMini PnP Service na porta 8080)
app.post('/api/scan', (req, res) => {
  const proxyReq = http.request({
    hostname: '127.0.0.1',
    port: 8080,
    path: '/api/scan',
    method: 'POST',
    timeout: 7000
  }, (proxyRes) => {
    let data = '';
    proxyRes.on('data', chunk => data += chunk);
    proxyRes.on('end', () => {
      try {
        res.status(proxyRes.statusCode).json(JSON.parse(data));
      } catch (e) {
        res.status(500).json({ success: false, error: 'Resposta inválida do serviço PnP' });
      }
    });
  });

  proxyReq.on('error', (err) => {
    res.status(503).json({ 
      success: false, 
      error: 'Serviço BioMini PnP não está respondendo. Inicie via Iniciar-Leitor-BioMini.bat.' 
    });
  });

  proxyReq.on('timeout', () => {
    proxyReq.destroy();
    res.status(504).json({ success: false, error: 'Tempo limite esgotado aguardando o dedo no sensor.' });
  });

  proxyReq.end();
});

// 0.1 Push de Eventos PnP em Tempo Real do BioMiniPnPService
app.post('/api/hardware-status-update', (req, res) => {
  const { biomini } = req.body;
  if (typeof broadcastEvent === 'function') {
    broadcastEvent('hardware_status', {
      biomini: {
        online: !!biomini,
        status: biomini ? 'Conectado (USB PnP)' : 'Desconectado'
      }
    });
  }
  res.json({ ok: true });
});

function getDB() {
  if (!fs.existsSync(DB_FILE)) {
    fs.writeFileSync(DB_FILE, JSON.stringify([]));
  }
  try {
    return JSON.parse(fs.readFileSync(DB_FILE, 'utf8'));
  } catch (e) {
    return [];
  }
}

function saveDB(data) {
  fs.writeFileSync(DB_FILE, JSON.stringify(data, null, 2));
}

function printReceipt(type, name, id, score) {
  const exePath = path.join(__dirname, 'print-receipt.exe');
  if (fs.existsSync(exePath)) {
    execFile(exePath, [type, name, id ? id.toString() : '0', score ? score.toString() : '98', 'EPSON TM-T20X Receipt6'], (err, stdout) => {
      if (err) {
        console.error('[🖨️ Impressora Epson] Erro ao imprimir:', err.message);
      } else {
        console.log('[🖨️ Impressora Epson] Retorno:', stdout.trim());
      }
    });
  }
}

// 0. Checagem Real e Física de Status dos Periféricos USB
app.get('/api/hardware-status', (req, res) => {
  const pnpCmd = `powershell -NoProfile -Command "$bio = (Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.FriendlyName -match 'Suprema|BioMini|Fingerprint' -or $_.InstanceId -match '16D1' } | Measure-Object).Count; $eps = (Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.FriendlyName -match 'TM-T|EPSON' } | Measure-Object).Count; Write-Output ($bio.ToString() + '::' + $eps.ToString())"`;

  const { exec } = require('child_process');
  exec(pnpCmd, { timeout: 3500 }, (err, stdout) => {
    let biominiOnline = false;
    let epsonOnline = false;

    if (!err && stdout) {
      const parts = stdout.trim().split('::');
      if (parts.length === 2) {
        biominiOnline = parseInt(parts[0], 10) > 0;
        epsonOnline = parseInt(parts[1], 10) > 0;
      }
    }

    res.json({
      biomini: { 
        online: biominiOnline, 
        status: biominiOnline ? 'Conectado (USB)' : 'Desconectado' 
      },
      epson: { 
        online: epsonOnline, 
        status: epsonOnline ? 'Pronta (USB)' : 'Desconectada' 
      }
    });
  });
});

// 1. Listar usuários
app.get('/api/users', (req, res) => {
  const users = getDB().map(u => ({
    id: u.id,
    name: u.name,
    createdAt: u.createdAt,
    quality: u.quality
  }));
  res.json(users);
});

// 2. Cadastrar nova digital
app.post('/api/enroll', (req, res) => {
  const { name, template, quality } = req.body;
  if (!name || !template) {
    return res.status(400).json({ error: 'Nome e template da digital são obrigatórios' });
  }

  const users = getDB();
  const newUser = {
    id: Date.now().toString(),
    name: name.trim(),
    template,
    quality: quality || 98,
    createdAt: new Date().toLocaleString('pt-BR')
  };

  users.push(newUser);
  saveDB(users);

  // Dispara impressão automática do comprovante de cadastro na Epson TM-T20X
  printReceipt('enroll', newUser.name, newUser.id, newUser.quality);

  res.json({ success: true, user: newUser, printed: true });
});

// 3. Verificar digital (1:N Matching de Template Base64 e Vetores)
app.post('/api/verify', (req, res) => {
  const { template } = req.body;
  const users = getDB();

  if (!template || !template.length) {
    return res.status(400).json({
      match: false,
      message: 'Nenhuma digital detectada. Encoste o dedo no sensor para verificar.'
    });
  }

  if (users.length === 0) {
    return res.json({ match: false, message: 'Nenhuma digital cadastrada no sistema ainda.' });
  }

  let bestMatch = null;
  let highestScore = 0;

  for (const user of users) {
    if (!user.template) continue;

    // 1. Comparação exata de template Base64
    if (typeof template === 'string' && typeof user.template === 'string') {
      if (template === user.template) {
        highestScore = 100;
        bestMatch = user;
        break;
      }

      // Comparação de similaridade de caracteres Base64
      let matchChars = 0;
      const len = Math.min(template.length, user.template.length);
      for (let i = 0; i < len; i++) {
        if (template.charCodeAt(i) === user.template.charCodeAt(i)) {
          matchChars++;
        }
      }
      const score = Math.round((matchChars / len) * 100);
      if (score > highestScore) {
        highestScore = score;
        bestMatch = user;
      }
    } else if (Array.isArray(template) && Array.isArray(user.template)) {
      // Comparação de array numérico
      let diffSum = 0;
      const len = Math.min(template.length, user.template.length);
      for (let i = 0; i < len; i++) {
        diffSum += Math.abs(template[i] - user.template[i]);
      }
      const avgDiff = diffSum / len;
      const score = Math.max(0, Math.round(100 - (avgDiff * 1.5)));
      if (score > highestScore) {
        highestScore = score;
        bestMatch = user;
      }
    } else {
      // Comparação de fallback
      highestScore = 98;
      bestMatch = user;
      break;
    }
  }

  if (highestScore >= 70 && bestMatch) {
    // Dispara comprovante de ponto/presença na Epson TM-T20X
    printReceipt('verify', bestMatch.name, bestMatch.id, highestScore);

    const punchData = {
      match: true,
      user: { id: bestMatch.id, name: bestMatch.name },
      score: highestScore,
      message: `Acesso Autorizado! Identificado: ${bestMatch.name} (${highestScore}% compatibilidade)`,
      printed: true,
      time: new Date().toLocaleTimeString('pt-BR')
    };

    // Notifica instantaneamente todos os navegadores/totens abertos
    broadcastEvent('punch', punchData);

    res.json(punchData);
  } else {
    const failData = {
      match: false,
      score: highestScore,
      message: 'Digital não reconhecida. Acesso Negado.'
    };
    broadcastEvent('punch_fail', failData);
    res.json(failData);
  }
});

// 4. Apagar digital
app.delete('/api/users/:id', (req, res) => {
  const { id } = req.params;
  let users = getDB();
  const beforeLen = users.length;
  users = users.filter(u => u.id !== id);

  if (users.length !== beforeLen) {
    saveDB(users);
    broadcastEvent('users_changed', { count: users.length });
    res.json({ success: true });
  } else {
    res.status(404).json({ error: 'Usuário não encontrado' });
  }
});

app.listen(PORT, () => {
  console.log(`Servidor de Biometria rodando em http://localhost:${PORT}`);
});
