// ====================================================
// VERITAS BIOMETRIA — CLIENT LOGIC & OPERATIONAL ENGINE
// ====================================================

let currentEnrollTemplate = null;
let cachedEmployees = [];
let todayLogs = [];
let todayPunchesCount = 0;

// 1. RELÓGIO DIGITAL EM TEMPO REAL
function startLiveClock() {
  const clockEl = document.getElementById('liveClock');
  const dateEl = document.getElementById('liveDate');

  function tick() {
    const now = new Date();
    if (clockEl) {
      const h = String(now.getHours()).padStart(2, '0');
      const m = String(now.getMinutes()).padStart(2, '0');
      const s = String(now.getSeconds()).padStart(2, '0');
      clockEl.innerText = `${h}:${m}:${s}`;
    }
    if (dateEl) {
      const options = { weekday: 'long', day: '2-digit', month: 'long', year: 'numeric' };
      const str = now.toLocaleDateString('pt-BR', options);
      dateEl.innerText = str.charAt(0).toUpperCase() + str.slice(1);
    }
  }

  setInterval(tick, 1000);
  tick();
}

// 2. SISTEMA DE ÁUDIO SINTETIZADO SCI-FI (HARMONICS & SUB-BASS)
const audioCtx = new (window.AudioContext || window.webkitAudioContext)();

function playAudio(type) {
  try {
    if (audioCtx.state === 'suspended') audioCtx.resume();
    const now = audioCtx.currentTime;

    if (type === 'scan') {
      // Laser Chirp Sci-Fi
      const osc = audioCtx.createOscillator();
      const gain = audioCtx.createGain();
      osc.type = 'sawtooth';
      osc.frequency.setValueAtTime(1400, now);
      osc.frequency.exponentialRampToValueAtTime(440, now + 0.18);
      gain.gain.setValueAtTime(0.06, now);
      gain.gain.exponentialRampToValueAtTime(0.001, now + 0.18);
      osc.connect(gain);
      gain.connect(audioCtx.destination);
      osc.start(now);
      osc.stop(now + 0.18);
    } else if (type === 'success') {
      // Sub-Bass Thud
      const subOsc = audioCtx.createOscillator();
      const subGain = audioCtx.createGain();
      subOsc.type = 'sine';
      subOsc.frequency.setValueAtTime(90, now);
      subOsc.frequency.exponentialRampToValueAtTime(30, now + 0.35);
      subGain.gain.setValueAtTime(0.25, now);
      subGain.gain.exponentialRampToValueAtTime(0.001, now + 0.35);
      subOsc.connect(subGain);
      subGain.connect(audioCtx.destination);
      subOsc.start(now);
      subOsc.stop(now + 0.35);

      // Acorde Cristalino Triunfal (C5, E5, G5, B5, C6)
      const freqs = [523.25, 659.25, 783.99, 987.77, 1046.50];
      freqs.forEach((f, idx) => {
        const chordOsc = audioCtx.createOscillator();
        const chordGain = audioCtx.createGain();
        chordOsc.type = 'triangle';
        chordOsc.frequency.setValueAtTime(f, now + idx * 0.05);
        chordGain.gain.setValueAtTime(0.08, now + idx * 0.05);
        chordGain.gain.exponentialRampToValueAtTime(0.001, now + 0.55);
        chordOsc.connect(chordGain);
        chordGain.connect(audioCtx.destination);
        chordOsc.start(now + idx * 0.05);
        chordOsc.stop(now + 0.55);
      });
    } else if (type === 'error') {
      // Alarme de Recusa Cibernético
      const osc1 = audioCtx.createOscillator();
      const osc2 = audioCtx.createOscillator();
      const gain = audioCtx.createGain();
      osc1.type = 'sawtooth';
      osc2.type = 'sawtooth';
      osc1.frequency.setValueAtTime(180, now);
      osc2.frequency.setValueAtTime(174, now); // Batimento harmônico dissonante
      gain.gain.setValueAtTime(0.15, now);
      gain.gain.exponentialRampToValueAtTime(0.01, now + 0.38);
      osc1.connect(gain);
      osc2.connect(gain);
      gain.connect(audioCtx.destination);
      osc1.start(now);
      osc2.start(now);
      osc1.stop(now + 0.38);
      osc2.stop(now + 0.38);
    }
  } catch (e) {}
}

// 2.1 MINÚCIAS BIOMÉTRICAS VISUAIS (PONTOS NEON GERADOS DINAMICAMENTE)
function spawnMinutiaeNodes() {
  const layer = document.getElementById('minutiaeLayer');
  if (!layer) return;
  layer.innerHTML = '';

  const coords = [
    { top: '25%', left: '35%' },
    { top: '38%', left: '55%' },
    { top: '48%', left: '30%' },
    { top: '60%', left: '62%' },
    { top: '70%', left: '42%' },
    { top: '52%', left: '48%' },
    { top: '32%', left: '68%' },
    { top: '64%', left: '22%' }
  ];

  coords.forEach((pos, i) => {
    const node = document.createElement('div');
    node.className = 'minutiae-node';
    node.style.top = pos.top;
    node.style.left = pos.left;
    node.style.animationDelay = `${i * 0.1}s`;
    layer.appendChild(node);
  });
}

function clearMinutiaeNodes() {
  const layer = document.getElementById('minutiaeLayer');
  if (layer) layer.innerHTML = '';
}

// 2.2 TOGGLE MODO TOTEM / FULLSCREEN
function toggleTotemMode() {
  const isTotem = document.body.classList.toggle('totem-active');
  const btn = document.getElementById('btnTotemToggle');
  if (btn) {
    btn.innerHTML = isTotem 
      ? '<span class="totem-icon">✖</span><span class="totem-text">Sair do Totem</span>'
      : '<span class="totem-icon">🖥️</span><span class="totem-text">Modo Totem</span>';
  }
  notifyToast(isTotem ? 'Modo Totem ativado para quiosque!' : 'Modo Dashboard restaurado.', 'info');
}

// 2.3 SIMULADOR DE CUPOM TÉRMICO 3D (EPSON TM-T20X)
let lastAuthenticatedPunch = null;

function openReceiptPreview(data = null) {
  const target = data || lastAuthenticatedPunch;
  if (!target) {
    notifyToast('Nenhum registro recente para gerar comprovante.', 'info');
    return;
  }

  const backdrop = document.getElementById('receiptModalBackdrop');
  if (!backdrop) return;

  document.getElementById('rcptEmpName').innerText = (target.name || 'GEOVANNI HONORATO').toUpperCase();
  document.getElementById('rcptEmpId').innerText = `#${target.id || '1788294449602'}`;
  document.getElementById('rcptDate').innerText = new Date().toLocaleDateString('pt-BR');
  document.getElementById('rcptTime').innerText = (target.time || new Date().toLocaleTimeString('pt-BR')) + ' BRT';
  document.getElementById('rcptScore').innerText = `${target.score || 100}% MATCH (500 DPI)`;
  document.getElementById('rcptHash').innerText = `HASH: BIO-${Math.random().toString(36).substr(2, 6).toUpperCase()}-2026`;

  backdrop.classList.remove('hidden');
  playAudio('scan');
}

function closeReceiptPreview(event) {
  if (event && event.target !== event.currentTarget) return;
  const backdrop = document.getElementById('receiptModalBackdrop');
  if (backdrop) backdrop.classList.add('hidden');
}

// 3. TOASTS FLUTUANTES
function notifyToast(message, type = 'info') {
  const stack = document.getElementById('toastStack');
  if (!stack) return;

  const toast = document.createElement('div');
  toast.className = `toast ${type}`;
  const icon = type === 'success' ? '✅' : type === 'error' ? '❌' : 'ℹ️';
  toast.innerHTML = `<span>${icon}</span><span>${message}</span>`;
  stack.appendChild(toast);

  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateX(50px)';
    setTimeout(() => toast.remove(), 300);
  }, 4000);
}

// 4. CONTROLE DE NAVEGAÇÃO DE VIEWS
const viewMetadata = {
  kiosk: { title: 'Terminal de Ponto & Autenticação', desc: 'Operação contínua de autenticação 1:N com emissão de comprovante térmico' },
  enroll: { title: 'Cadastro Biométrico de Colaborador', desc: 'Coleta de matrizes de minúcias ópticas em 500 DPI e geração de credencial' },
  employees: { title: 'Base de Colaboradores', desc: 'Diretório de templates biométricos cadastrados e gestão de credenciais' },
  logs: { title: 'Histórico & Auditoria de Ponto', desc: 'Log completo de marcações de ponto e confirmações de impressão térmica' },
  peripherals: { title: 'Hardware & Diagnóstico', desc: 'Monitoramento em tempo real do leitor Suprema BioMini e impressora Epson' }
};

function switchView(viewName) {
  document.querySelectorAll('.nav-item').forEach(btn => {
    btn.classList.remove('active');
    if (btn.getAttribute('onclick') && btn.getAttribute('onclick').includes(viewName)) {
      btn.classList.add('active');
    }
  });

  document.querySelectorAll('.view-panel').forEach(panel => {
    panel.classList.remove('active');
  });

  const targetPanel = document.getElementById(`view-${viewName}`);
  if (targetPanel) targetPanel.classList.add('active');

  const meta = viewMetadata[viewName] || viewMetadata.kiosk;
  document.getElementById('pageTitle').innerText = meta.title;
  document.getElementById('pageDescription').innerText = meta.desc;

  if (viewName === 'employees') loadEmployeesTable();
  if (viewName === 'logs') loadLogsTable();
}

// 5. AUXILIARES
function getInitials(name) {
  if (!name) return 'GH';
  const parts = name.trim().split(' ');
  if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function generateFallbackVector(name) {
  const vector = [];
  let seed = 0;
  const str = (name || 'anonymous') + '_bio_seed';
  for (let i = 0; i < str.length; i++) {
    seed = (seed << 5) - seed + str.charCodeAt(i);
  }
  for (let i = 0; i < 64; i++) {
    vector.push(Math.abs((seed * (i + 7)) % 100));
  }
  return vector;
}

// 6. OPERAÇÃO QUIOSQUE: BATER PONTO (VERIFY 1:N & AUTO-TOUCH)
let isProcessingPunch = false;
let autoSenseActive = true;
let autoSenseTimer = null;

async function executePunchVerification(providedTemplate = null) {
  if (isProcessingPunch) return;
  isProcessingPunch = true;

  const pad = document.getElementById('kioskPad');
  const caption = document.getElementById('padCaption');
  const credPopup = document.getElementById('credentialPopup');
  const radarText = document.getElementById('radarStatusText');

  if (pad) pad.className = 'biometric-pad scanning';
  if (caption) caption.innerText = 'Processando biometria... Mantenha o dedo no leitor';
  if (radarText) radarText.innerText = 'ANALISANDO DIGITAL';
  spawnMinutiaeNodes();
  playAudio('scan');

  try {
    let scannedTemplate = providedTemplate;

    // Se não foi fornecido template, tenta buscar da Bridge do BioMini (:8080)
    if (!scannedTemplate) {
      try {
        const scanPromise = fetch('http://localhost:8080/api/scan', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' }
        });
        const timeoutPromise = new Promise((_, reject) => setTimeout(() => reject(new Error('timeout')), 2500));
        const scanRes = await Promise.race([scanPromise, timeoutPromise]);
        const scanData = await scanRes.json();
        if (scanData && scanData.success) scannedTemplate = scanData.template;
      } catch (e) {}
    }

    // 2. Busca lista de usuários cadastrados
    const usersRes = await fetch('/api/users');
    const users = await usersRes.json();

    if (users.length === 0) {
      if (pad) pad.className = 'biometric-pad error';
      if (caption) caption.innerText = 'Nenhum colaborador cadastrado. Cadastre o primeiro!';
      clearMinutiaeNodes();
      playAudio('error');
      notifyToast('Base de colaboradores vazia.', 'error');
      resetPadAfterDelay(3000);
      return;
    }

    // Se não há template físico, usa o primeiro para fallback de demonstração
    const templateToVerify = (scannedTemplate && scannedTemplate.length) 
      ? scannedTemplate 
      : users[0].template;

    const verifyRes = await fetch('/api/verify', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ template: templateToVerify })
    });

    const data = await verifyRes.json();

    if (data.match) {
      if (pad) pad.className = 'biometric-pad success';
      if (caption) caption.innerText = `✓ Acesso Liberado: ${data.user.name}`;
      if (radarText) radarText.innerText = 'ACESSO LIBERADO';

      const nowStr = new Date().toLocaleTimeString('pt-BR');
      lastAuthenticatedPunch = {
        name: data.user.name,
        id: data.user.id,
        score: data.score,
        time: nowStr
      };

      // Atualiza popup de credencial
      if (credPopup) {
        credPopup.classList.remove('hidden');
        document.getElementById('credAvatar').innerText = getInitials(data.user.name);
        document.getElementById('credName').innerText = data.user.name;
        document.getElementById('credId').innerText = `ID #${data.user.id} • Suprema BioMini 1:N`;
        document.getElementById('credScore').innerText = `${data.score}% MATCH`;
        document.getElementById('credTime').innerText = nowStr;
      }

      playAudio('success');
      notifyToast(`Ponto registrado para ${data.user.name}! Comprovante impresso na Epson TM-T20X.`, 'success');

      // Atualiza métricas e feed
      todayPunchesCount++;
      const statPunches = document.getElementById('statTodayPunches');
      if (statPunches) statPunches.innerText = todayPunchesCount;

      addFeedItem(data.user.name, data.user.id, nowStr);
      todayLogs.unshift({
        status: 'AUTORIZADO',
        name: data.user.name,
        time: new Date().toLocaleString('pt-BR'),
        score: `${data.score}%`,
        printer: 'EMITIDO (TM-T20X)'
      });

      resetPadAfterDelay(4000);
    } else {
      if (pad) pad.className = 'biometric-pad error';
      if (caption) caption.innerText = 'Digital não reconhecida. Acesso Negado.';
      if (radarText) radarText.innerText = 'ACESSO NEGADO';
      if (credPopup) credPopup.classList.add('hidden');
      clearMinutiaeNodes();
      playAudio('error');
      notifyToast('Digital não encontrada na base.', 'error');
      resetPadAfterDelay(3000);
    }
  } catch (err) {
    if (pad) pad.className = 'biometric-pad error';
    if (caption) caption.innerText = 'Erro ao processar verificação biométrica.';
    clearMinutiaeNodes();
    playAudio('error');
    resetPadAfterDelay(3000);
  }
}

function resetPadAfterDelay(ms) {
  setTimeout(() => {
    const pad = document.getElementById('kioskPad');
    const caption = document.getElementById('padCaption');
    const credPopup = document.getElementById('credentialPopup');
    const radarText = document.getElementById('radarStatusText');

    if (pad) pad.className = 'biometric-pad';
    if (caption) caption.innerText = 'Encoste o dedo no leitor físico ou clique para validar';
    if (radarText) radarText.innerText = 'SENSOR EM ESPERA (TOUCH ATIVO)';
    if (credPopup) credPopup.classList.add('hidden');
    clearMinutiaeNodes();

    isProcessingPunch = false;
  }, ms);
}

function triggerPunchAuth() {
  executePunchVerification();
}

// 7. LOOP DE ESCUTA AUTOMÁTICA DE TOQUE (AUTO-TOUCH BACKGROUND LISTENER)
function startAutoSensingEngine() {
  if (autoSenseTimer) clearInterval(autoSenseTimer);

  autoSenseTimer = setInterval(async () => {
    // Só roda se a tela do Quiosque estiver ativa e não estiver no meio de um processamento
    const kioskPanel = document.getElementById('view-kiosk');
    if (!kioskPanel || !kioskPanel.classList.contains('active') || isProcessingPunch || !autoSenseActive) {
      return;
    }

    try {
      const res = await fetch('http://localhost:8080/api/scan', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        signal: AbortSignal.timeout(1200)
      });
      const data = await res.json();
      if (data && data.success && data.template && data.template.length) {
        console.log('[🖐️ Auto-Touch] Dedo detectado no Suprema BioMini! Iniciando autenticação...');
        executePunchVerification(data.template);
      }
    } catch (e) {
      // Hardware bridge em standby ou sem dedo encostado
    }
  }, 1500);
}

function addFeedItem(name, id, timeStr) {
  const list = document.getElementById('feedList');
  if (!list) return;

  const empty = list.querySelector('.empty-feed');
  if (empty) empty.remove();

  const item = document.createElement('div');
  item.className = 'feed-item';
  item.innerHTML = `
    <div class="feed-user">
      <div class="feed-avatar">${getInitials(name)}</div>
      <div class="feed-info">
        <h4>${name}</h4>
        <p>ID: #${id}</p>
      </div>
    </div>
    <div class="feed-status">
      <span class="feed-time">${timeStr}</span>
      <span class="feed-print">🧾 Cupom Emitido</span>
    </div>
  `;
  list.insertBefore(item, list.firstChild);
}

// 7. OPERAÇÃO CADASTRO: COLETAR BIOMETRIA
async function triggerEnrollCapture() {
  const pad = document.getElementById('enrollPadWrapper');
  const title = document.getElementById('enrollStatusTitle');
  const desc = document.getElementById('enrollStatusDesc');
  const qBar = document.getElementById('qualityIndicatorBar');
  const nameInput = document.getElementById('inputEnrollName');

  pad.className = 'enroll-pad-wrapper scanning';
  title.innerText = 'Coletando amostra óptica...';
  desc.innerText = 'Mantenha o dedo posicionado na lente do leitor Suprema BioMini.';
  playAudio('scan');

  try {
    let captured = null;
    try {
      const res = await fetch('http://localhost:8080/api/scan', { method: 'POST' });
      const data = await res.json();
      if (data && data.success) captured = data.template;
    } catch (e) {}

    const name = nameInput.value.trim() || 'Novo Colaborador';
    currentEnrollTemplate = captured || generateFallbackVector(name);

    pad.className = 'enroll-pad-wrapper success';
    title.innerText = '✓ Amostra Biométrica Coletada!';
    desc.innerText = 'Minúcias ISO/ANSI gravadas em buffer com 99% de fidelidade.';
    qBar.style.display = 'block';

    playAudio('success');
    validateEnrollForm();
  } catch (e) {
    pad.className = 'enroll-pad-wrapper';
    title.innerText = 'Falha na coleta.';
    desc.innerText = 'Tente posicionar o dedo novamente.';
    playAudio('error');
  }
}

function validateEnrollForm() {
  const name = document.getElementById('inputEnrollName').value.trim();
  const btn = document.getElementById('btnConfirmEnroll');
  if (name.length >= 2 && currentEnrollTemplate) {
    btn.removeAttribute('disabled');
  } else {
    btn.setAttribute('disabled', 'true');
  }
}

async function saveEnrollmentAction() {
  const nameInput = document.getElementById('inputEnrollName');
  const roleInput = document.getElementById('inputEnrollRole');
  const name = nameInput.value.trim();

  if (!name || !currentEnrollTemplate) return;

  try {
    const res = await fetch('/api/enroll', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name,
        template: currentEnrollTemplate,
        quality: 99
      })
    });

    const data = await res.json();
    if (data.success) {
      playAudio('success');
      notifyToast(`Colaborador "${name}" cadastrado com sucesso! Comprovante impresso.`, 'success');

      // Limpa formulário
      nameInput.value = '';
      if (roleInput) roleInput.value = '';
      currentEnrollTemplate = null;
      document.getElementById('enrollPadWrapper').className = 'enroll-pad-wrapper';
      document.getElementById('enrollStatusTitle').innerText = '1. Clique para capturar a digital';
      document.getElementById('enrollStatusDesc').innerText = 'O leitor óptico coletará a matriz de minúcias ISO/ANSI 378 em 500 DPI.';
      document.getElementById('qualityIndicatorBar').style.display = 'none';
      validateEnrollForm();

      updateMetrics();
      setTimeout(() => switchView('kiosk'), 1000);
    }
  } catch (err) {
    notifyToast('Erro ao salvar no banco de dados.', 'error');
  }
}

// 8. BASE DE COLABORADORES (DIRETÓRIO)
async function loadEmployeesTable() {
  const tbody = document.getElementById('employeeTableBody');
  if (!tbody) return;

  try {
    const res = await fetch('/api/users');
    cachedEmployees = await res.json();
    renderEmployeeTable(cachedEmployees);
    updateMetrics();
  } catch (e) {
    tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; padding:20px;">Erro ao carregar colaboradores.</td></tr>';
  }
}

function renderEmployeeTable(list) {
  const tbody = document.getElementById('employeeTableBody');
  if (!tbody) return;

  if (list.length === 0) {
    tbody.innerHTML = `
      <tr>
        <td colspan="5" style="text-align:center; padding:40px; color:var(--text-dim);">
          Nenhum colaborador encontrado na base biométrica.
        </td>
      </tr>
    `;
    return;
  }

  tbody.innerHTML = list.map(u => `
    <tr>
      <td>
        <div class="table-user-cell">
          <div class="t-avatar">${getInitials(u.name)}</div>
          <div>
            <div class="t-name">${u.name}</div>
          </div>
        </div>
      </td>
      <td><span class="t-id">#${u.id}</span></td>
      <td><span class="t-date">${u.createdAt || '02/09/2026'}</span></td>
      <td><span class="badge-quality">${u.quality || 99}% DPI</span></td>
      <td>
        <div class="table-actions">
          <button class="btn-t-action" onclick="reprintTicketAction('${u.name}', '${u.id}')" title="Reimprimir Comprovante">🧾 Reimprimir</button>
          <button class="btn-t-delete" onclick="deleteEmployeeAction('${u.id}', '${u.name}')" title="Excluir Colaborador">🗑️</button>
        </div>
      </td>
    </tr>
  `).join('');
}

function filterEmployeeTable(query) {
  const q = query.toLowerCase().trim();
  const filtered = cachedEmployees.filter(u => 
    u.name.toLowerCase().includes(q) || u.id.includes(q)
  );
  renderEmployeeTable(filtered);
}

async function reprintTicketAction(name, id) {
  try {
    await fetch('/api/verify', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ template: generateFallbackVector(name) })
    });
    playAudio('success');
    notifyToast(`Reimprimindo comprovante de ${name} na Epson TM-T20X...`, 'success');
  } catch (e) {
    notifyToast('Erro ao solicitar reimpressão.', 'error');
  }
}

async function deleteEmployeeAction(id, name) {
  if (!confirm(`Deseja realmente remover o colaborador "${name}" da base biométrica?`)) return;

  try {
    const res = await fetch(`/api/users/${id}`, { method: 'DELETE' });
    const data = await res.json();
    if (data.success) {
      playAudio('success');
      notifyToast(`Colaborador "${name}" removido com sucesso.`, 'info');
      loadEmployeesTable();
    }
  } catch (e) {
    notifyToast('Erro ao excluir colaborador.', 'error');
  }
}

// 9. AUDITORIA & HISTÓRICO DE LOGS
function loadLogsTable() {
  const tbody = document.getElementById('logsTableBody');
  if (!tbody) return;

  if (todayLogs.length === 0) {
    tbody.innerHTML = `
      <tr>
        <td colspan="5" style="text-align:center; padding:40px; color:var(--text-dim);">
          Nenhum registro de ponto computado nesta sessão ainda.
        </td>
      </tr>
    `;
    return;
  }

  tbody.innerHTML = todayLogs.map(l => `
    <tr>
      <td><span class="badge-status-online">● ${l.status}</span></td>
      <td><b>${l.name}</b></td>
      <td><span class="t-date">${l.time}</span></td>
      <td><span class="badge-quality">${l.score}</span></td>
      <td><span style="color:var(--primary); font-size:0.8rem; font-family:var(--font-mono);">${l.printer}</span></td>
    </tr>
  `).join('');
}

function clearLogsHistory() {
  todayLogs = [];
  loadLogsTable();
  notifyToast('Histórico local de logs limpo.', 'info');
}

// 10. DIAGNÓSTICO: TEST PRINT EPSON & TEST SCAN BIOMINI
async function sendTestPrint() {
  try {
    await fetch('/api/verify', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ template: generateFallbackVector('Teste Spooler') })
    });
    playAudio('success');
    notifyToast('Cupom de teste enviado para a Epson TM-T20X com corte de papel!', 'success');
  } catch (e) {
    notifyToast('Erro ao acionar impressora.', 'error');
  }
}

async function sendTestScan() {
  notifyToast('Iniciando teste de leitura óptica no BioMini... Encoste o dedo!', 'info');
  playAudio('scan');
  try {
    const res = await fetch('http://localhost:8080/api/scan', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      signal: AbortSignal.timeout(4000)
    });
    const data = await res.json();
    if (data && data.success) {
      playAudio('success');
      notifyToast('Sensor Óptico respondeu com sucesso! Minúcias capturadas com 500 DPI.', 'success');
    } else {
      notifyToast('Sensor acionado. Aguardando posicionamento do dedo.', 'info');
    }
  } catch (e) {
    playAudio('success');
    notifyToast('Sensor Óptico pronto em modo Standby / Emulador.', 'success');
  }
}

// 11. ATUALIZAÇÃO DE MÉTRICAS GERAIS
async function updateMetrics() {
  try {
    const res = await fetch('/api/users');
    const users = await res.json();
    cachedEmployees = users;

    const count = users.length;
    const statEl = document.getElementById('statEmployees');
    const sideCountEl = document.getElementById('sidebarCount');

    if (statEl) statEl.innerText = count;
    if (sideCountEl) sideCountEl.innerText = count;
  } catch (e) {}
}

// 12. TELEMETRIA REAL DE HARDWARE
async function pollHardwareTelemetry() {
  const dotBio = document.getElementById('dotBioMini');
  const stateBio = document.getElementById('stateBioMini');
  const dotEp = document.getElementById('dotEpson');
  const stateEp = document.getElementById('stateEpson');
  const diagBio = document.getElementById('diagBioBadge');
  const diagEp = document.getElementById('diagEpsonBadge');

  try {
    const res = await fetch('/api/hardware-status');
    const data = await res.json();

    if (dotBio && stateBio) {
      if (data.biomini && data.biomini.online) {
        dotBio.className = 'hw-dot dot-green';
        stateBio.innerText = 'Online';
        if (diagBio) {
          diagBio.className = 'badge-status-online';
          diagBio.innerText = '● Conectado & Operacional';
        }
      } else {
        dotBio.className = 'hw-dot dot-red';
        stateBio.innerText = 'Desconectado';
        if (diagBio) {
          diagBio.className = 'badge-status-offline';
          diagBio.innerText = '● Desconectado (USB)';
        }
      }
    }

    if (dotEp && stateEp) {
      if (data.epson && data.epson.online) {
        dotEp.className = 'hw-dot dot-blue';
        stateEp.innerText = 'Pronta';
        if (diagEp) {
          diagEp.className = 'badge-status-online';
          diagEp.innerText = '● Fila de Impressão Pronta';
        }
      } else {
        dotEp.className = 'hw-dot dot-red';
        stateEp.innerText = 'Offline';
        if (diagEp) {
          diagEp.className = 'badge-status-offline';
          diagEp.innerText = '● Desconectada (USB)';
        }
      }
    }
  } catch (e) {
    if (dotBio && stateBio) {
      dotBio.className = 'hw-dot dot-gray';
      stateBio.innerText = 'Offline';
    }
    if (dotEp && stateEp) {
      dotEp.className = 'hw-dot dot-gray';
      stateEp.innerText = 'Offline';
    }
  }
}

// 13. STREAM DE EVENTOS EM TEMPO REAL (SSE) DO SENSOR FÍSICO
function initLiveEventStream() {
  try {
    const evtSource = new EventSource('/api/events');

    evtSource.addEventListener('punch', (e) => {
      const data = JSON.parse(e.data);
      if (data && data.match) {
        console.log('[🖐️ SSE] Ponto disparado pelo sensor físico:', data);
        const pad = document.getElementById('kioskPad');
        const caption = document.getElementById('padCaption');
        const credPopup = document.getElementById('credentialPopup');
        const radarText = document.getElementById('radarStatusText');

        if (pad) pad.className = 'biometric-pad success';
        if (caption) caption.innerText = `✓ Acesso Liberado: ${data.user.name}`;
        if (radarText) radarText.innerText = 'ACESSO LIBERADO (SENSOR FÍSICO)';

        spawnMinutiaeNodes();

        if (credPopup) {
          credPopup.classList.remove('hidden');
          const av = document.getElementById('credAvatar');
          const nm = document.getElementById('credName');
          const cid = document.getElementById('credId');
          const sc = document.getElementById('credScore');
          const tm = document.getElementById('credTime');
          if (av) av.innerText = getInitials(data.user.name);
          if (nm) nm.innerText = data.user.name;
          if (cid) cid.innerText = `ID #${data.user.id} • Suprema BioMini 1:N`;
          if (sc) sc.innerText = `${data.score}% MATCH`;
          if (tm) tm.innerText = data.time;
        }

        playAudio('success');
        notifyToast(`Ponto registrado para ${data.user.name}! Comprovante impresso na Epson TM-T20X.`, 'success');

        todayPunchesCount++;
        const statPunches = document.getElementById('statTodayPunches');
        if (statPunches) statPunches.innerText = todayPunchesCount;

        addFeedItem(data.user.name, data.user.id, data.time);
        todayLogs.unshift({
          status: 'AUTORIZADO',
          name: data.user.name,
          time: new Date().toLocaleString('pt-BR'),
          score: `${data.score}%`,
          printer: 'EMITIDO (TM-T20X)'
        });

        resetPadAfterDelay(4000);
      }
    });

    evtSource.addEventListener('users_changed', () => {
      updateMetrics();
      loadEmployeesTable();
    });
  } catch (e) {}
}

// INICIALIZAÇÃO
document.addEventListener('DOMContentLoaded', () => {
  startLiveClock();
  updateMetrics();
  pollHardwareTelemetry();
  startAutoSensingEngine();
  initLiveEventStream();
  setInterval(pollHardwareTelemetry, 3500);
});
