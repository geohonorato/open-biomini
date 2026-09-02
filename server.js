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

let printingEnabled = true;

function printReceipt(type, name, id, score) {
  if (!printingEnabled) {
    console.log(`[🖨️ Impressora Epson] Impressão FÍSICA PAUSADA (Modo Economia). Comprovante virtual emitido para: ${name}`);
    return;
  }
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

// 0. Checagem Real e Física de Status dos Periféricos USB com Cache Inteligente
let cachedHardwareStatus = {
  biomini: { online: true, status: 'Conectado (USB)' },
  epson: { online: true, status: 'Pronta (USB)' },
  lastChecked: 0
};

app.get('/api/hardware-status', (req, res) => {
  const now = Date.now();
  if (now - cachedHardwareStatus.lastChecked < 6000) {
    return res.json(cachedHardwareStatus);
  }

  const pnpCmd = `powershell -NoProfile -Command "$bio = (Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.FriendlyName -match 'Suprema|BioMini|Fingerprint' -or $_.InstanceId -match '16D1' } | Measure-Object).Count; $eps = (Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.FriendlyName -match 'TM-T|EPSON' } | Measure-Object).Count; Write-Output ($bio.ToString() + '::' + $eps.ToString())"`;

  const { exec } = require('child_process');
  exec(pnpCmd, { timeout: 4000, maxBuffer: 1024 * 1024 }, (err, stdout) => {
    let biominiOnline = cachedHardwareStatus.biomini.online;
    let epsonOnline = cachedHardwareStatus.epson.online;

    if (!err && stdout) {
      const parts = stdout.trim().split('::');
      if (parts.length === 2) {
        biominiOnline = parseInt(parts[0], 10) > 0;
        epsonOnline = parseInt(parts[1], 10) > 0;
      }
    }

    cachedHardwareStatus = {
      biomini: { 
        online: biominiOnline, 
        status: biominiOnline ? 'Conectado (USB)' : 'Desconectado' 
      },
      epson: { 
        online: epsonOnline, 
        status: epsonOnline ? 'Pronta (USB)' : 'Desconectada' 
      },
      lastChecked: now
    };

    res.json(cachedHardwareStatus);
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

// 3. Verificar digital (1:N Biometric Matching Real via OpenBioMiniService / UFMatcher)
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

  const validUsers = users.filter(u => u.template && u.template.length > 50);
  const candidates = validUsers.map(u => u.template);

  const payloadData = JSON.stringify({
    probe: template,
    templates: candidates
  });

  const identifyReq = http.request({
    hostname: '127.0.0.1',
    port: 8080,
    path: '/api/identify',
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Content-Length': Buffer.byteLength(payloadData)
    },
    timeout: 4000
  }, (identifyRes) => {
    let data = '';
    identifyRes.on('data', chunk => data += chunk);
    identifyRes.on('end', () => {
      try {
        const matchResult = JSON.parse(data);
        if (matchResult.matched && matchResult.matchIndex >= 0 && matchResult.matchIndex < validUsers.length) {
          const bestMatch = validUsers[matchResult.matchIndex];
          const highestScore = matchResult.score || 98;

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

          broadcastEvent('punch', punchData);
          return res.json(punchData);
        } else {
          return res.json({
            match: false,
            score: 0,
            message: 'Digital não reconhecida. Tente novamente ou cadastre o colaborador.',
            time: new Date().toLocaleTimeString('pt-BR')
          });
        }
      } catch (e) {
        return res.status(500).json({ match: false, message: 'Erro no processamento biométrico.' });
      }
    });
  });

  identifyReq.on('error', (err) => {
    return res.status(503).json({ match: false, message: 'Serviço biométrico OpenBioMiniService offline na porta 8080.' });
  });

  identifyReq.on('timeout', () => {
    identifyReq.destroy();
    return res.status(504).json({ match: false, message: 'Tempo limite esgotado no matching biométrico.' });
  });

  identifyReq.write(payloadData);
  identifyReq.end();
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

// 5. Impressão de cupom avulso / teste na Epson TM-T20X
app.post('/api/print', (req, res) => {
  const { type, name, id, score } = req.body;
  printReceipt(type || 'verify', name || 'Teste Spooler', id || '0', score || 98);
  res.json({ success: true, printed: printingEnabled });
});

// 6. Consultar e Alternar Pausa de Impressão Física
app.get('/api/printer/status', (req, res) => {
  res.json({ enabled: printingEnabled });
});

app.post('/api/printer/toggle', (req, res) => {
  const body = req.body || {};
  if (typeof body.enabled === 'boolean') {
    printingEnabled = body.enabled;
  } else {
    printingEnabled = !printingEnabled;
  }
  if (typeof broadcastEvent === 'function') {
    broadcastEvent('printer_status', { enabled: printingEnabled });
  }
  res.json({ success: true, enabled: printingEnabled });
});

app.listen(PORT, () => {
  console.log(`Servidor de Biometria rodando em http://localhost:${PORT}`);
});
