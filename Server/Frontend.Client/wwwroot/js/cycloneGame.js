// Cyclone Escape — Canvas game for Blazor
// Place at: wwwroot/js/cycloneGame.js

let cvs, ctx, dotnet;
let rafId = 0;
let running = false, paused = false, showHelp = true;

const S = {
  groundH: 80,
  baseSpeed: 2.2,
  maxSpeed: 8.5,
  gravity: 0.85,
  jumpPower: 15.5,
  obstacleGapMin: 280,
  obstacleGapVar: 240
};

const player = { x: 110, y: 0, w: 40, h: 40, vy: 0, onGround: true, bounceOffset: 0 };
let score = 0, highScore = 0, speed = S.baseSpeed;
let speedThreshold = 50; // Track next speed increase threshold
let obstacles = [];
let flyingObstacles = [];
let clouds = [];
let particles = [];
let notifications = [];
let nextObstacleDistance = 0;
let gameOverScreen = false;

function resize() {
  cvs.width = cvs.clientWidth;
  cvs.height = cvs.clientHeight;
  player.y = cvs.height - S.groundH - player.h;
}

function addClouds() {
  clouds = [];
  for (let i = 0; i < 8; i++) {
    clouds.push({
      x: Math.random() * cvs.width,
      y: 50 + Math.random() * 180,
      sp: 0.4 + Math.random() * 0.7
    });
  }
}

function spawnObstacle(force=false) {
  if (!force && obstacles.length) {
    const last = obstacles[obstacles.length - 1];
    if (last.x > cvs.width - nextObstacleDistance) return;
  }
  
  // Improved speed-scaled obstacle spacing
  const baseGap = S.obstacleGapMin + Math.random() * S.obstacleGapVar;
  const speedRatio = S.baseSpeed / speed;
  
  // Better scaling: maintain challenge while keeping playability
  // At base speed: normal gaps, at max speed: 60% of normal gaps
  const speedScale = Math.max(0.6, Math.min(1.0, speedRatio));
  const randomVariation = 0.7 + Math.random() * 0.6; // 70% to 130% variation
  
  nextObstacleDistance = baseGap * speedScale * randomVariation;
  
  // Ensure minimum gap for playability at high speeds
  nextObstacleDistance = Math.max(200, nextObstacleDistance);
  
  const types = ['🌳','🪨','🌵','🚧'];
  const newObstacle = {
    x: cvs.width + 20,
    y: cvs.height - S.groundH - 50,
    w: 40, h: 50,
    type: types[Math.floor(Math.random()*types.length)],
    spawnAnim: 1.0 // Animation scale for spawn effect
  };
  
  obstacles.push(newObstacle);
  
  // Speed-adjusted flying obstacle spawning
  const flyingChance = Math.min(0.25, 0.1 + (speed - S.baseSpeed) * 0.02); // Increase chance with speed
  if (Math.random() < flyingChance && score > 50) {
    const delay = Math.random() * 1500 + 800; // Shorter delays at higher speeds
    setTimeout(() => spawnFlyingObstacle(), delay);
  }
}

function spawnFlyingObstacle() {
  const flyingTypes = ['🦅','🦇','🐦','🪶'];
  
  // Calculate player's jump height (like Chrome Dino pterodactyls)
  // Player jumps to approximately this height at peak
  const jumpPeakHeight = cvs.height - S.groundH - player.h - (S.jumpPower * 8); // Approximate jump peak
  
  flyingObstacles.push({
    x: cvs.width + 50,
    y: jumpPeakHeight, // Fixed height like Chrome Dino - no random variation
    w: 35, h: 25,
    type: flyingTypes[Math.floor(Math.random()*flyingTypes.length)],
    warning: true, // Show warning indicator
    spawnAnim: 1.0
  });
}

function drawBg() {
  // sky gradient
  const g = ctx.createLinearGradient(0, 0, 0, cvs.height);
  g.addColorStop(0, '#87CEEB'); g.addColorStop(0.7, '#98FB98'); g.addColorStop(1, '#8FBC8F');
  ctx.fillStyle = g; ctx.fillRect(0,0,cvs.width,cvs.height);

  // clouds
  ctx.font = '24px system-ui';
  clouds.forEach(c => {
    ctx.fillText('☁️', c.x, c.y);
    c.x -= c.sp;
    if (c.x < -60) { c.x = cvs.width + 40; c.y = 50 + Math.random()*180; }
  });

  // ground
  ctx.fillStyle = '#228B22';
  ctx.fillRect(0, cvs.height - S.groundH, cvs.width, S.groundH);
  ctx.fillStyle = '#8B4513';
  ctx.fillRect(0, cvs.height - S.groundH, cvs.width, 3);
}

function drawPlayer() {
  ctx.save();
  
  // Add bounce animation when landing
  if (player.onGround && player.bounceOffset > 0) {
    player.bounceOffset *= 0.85; // Smooth decay
    if (player.bounceOffset < 0.5) player.bounceOffset = 0;
  }
  
  ctx.scale(-1, 1); // Flip horizontally to face right
  ctx.font = '40px system-ui';
  ctx.fillText('🚴‍♂️', -player.x - player.w, player.y + player.h - player.bounceOffset);
  ctx.restore();
}

// Particle system for visual effects
function createParticles(x, y, count, type = 'speed') {
  for (let i = 0; i < count; i++) {
    particles.push({
      x: x + Math.random() * 40 - 20,
      y: y + Math.random() * 40 - 20,
      vx: Math.random() * 4 - 2,
      vy: Math.random() * 4 - 2,
      life: 1.0,
      decay: 0.02 + Math.random() * 0.02,
      type: type,
      size: Math.random() * 8 + 4
    });
  }
}

function updateParticles() {
  for (let i = particles.length - 1; i >= 0; i--) {
    const p = particles[i];
    p.x += p.vx;
    p.y += p.vy;
    p.life -= p.decay;
    p.vy += 0.1; // Gravity
    
    if (p.life <= 0) {
      particles.splice(i, 1);
    }
  }
}

function drawParticles() {
  particles.forEach(p => {
    ctx.save();
    ctx.globalAlpha = p.life;
    ctx.fillStyle = p.type === 'speed' ? '#FFD700' : '#FF6B35';
    ctx.beginPath();
    ctx.arc(p.x, p.y, p.size * p.life, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  });
}

// Notification system for speed increases
function showNotification(text, duration = 2000) {
  notifications.push({
    text: text,
    life: 1.0,
    maxLife: 1.0,
    duration: duration,
    startTime: Date.now(),
    scale: 0.1
  });
}

function drawObstacles() {
  ctx.font = '40px system-ui';
  
  // Draw ground obstacles
  for (let i = obstacles.length - 1; i >= 0; i--) {
    const o = obstacles[i];
    
    // Animate spawn effect
    if (o.spawnAnim > 0) {
      o.spawnAnim -= 0.05;
      ctx.save();
      ctx.scale(1, Math.max(0.1, o.spawnAnim));
      ctx.fillText(o.type, o.x, o.y + o.h);
      ctx.restore();
    } else {
      ctx.fillText(o.type, o.x, o.y + o.h);
    }
    
    o.x -= (speed + 3.2);
    if (o.x + o.w < 0) {
      obstacles.splice(i,1);
      score += 10;
      
      // Improved speed ramping with visual feedback
      if (score >= speedThreshold && speed < S.maxSpeed) {
        const oldSpeed = speed;
        speed = Math.min(S.maxSpeed, speed + 0.6);
        speedThreshold += 50; // Set next threshold
        
        // Visual feedback for speed increase
        showNotification(`SPEED UP! ${speed.toFixed(1)}x`, 1500);
        createParticles(player.x + 20, player.y + 20, 15, 'speed');
        
        // Screen shake effect
        setTimeout(() => {
          cvs.style.transform = 'translate(2px, 1px)';
          setTimeout(() => cvs.style.transform = '', 100);
        }, 50);
      }
      
      safeSendScore();
    }
  }
  
  // Draw flying obstacles
  for (let i = flyingObstacles.length - 1; i >= 0; i--) {
    const f = flyingObstacles[i];
    
    // Show warning indicator - more accurate timing for straight-flying obstacles
    if (f.warning && f.x > cvs.width * 0.75 && f.x < cvs.width * 1.1) {
      ctx.save();
      ctx.fillStyle = 'rgba(255, 0, 0, 0.8)';
      ctx.font = 'bold 18px Arial';
      ctx.textAlign = 'center';
      ctx.fillText('⚠️ DON\'T JUMP! ⚠️', cvs.width / 2, 80);
      ctx.textAlign = 'start';
      ctx.restore();
    }
    
    // Animate spawn effect  
    if (f.spawnAnim > 0) {
      f.spawnAnim -= 0.03;
      ctx.save();
      ctx.scale(Math.max(0.1, f.spawnAnim), 1);
      ctx.fillText(f.type, f.x, f.y + f.h);
      ctx.restore();
    } else {
      // Flying straight like Chrome Dino pterodactyls - no bobbing
      ctx.fillText(f.type, f.x, f.y + f.h);
    }
    
    // Flying obstacles move at same speed as ground obstacles
    f.x -= (speed + 3.2);
    if (f.x + f.w < 0) {
      flyingObstacles.splice(i,1);
      score += 15; // More points for avoiding flying obstacles
      safeSendScore();
    }
  }
  
  spawnObstacle();
}

function rectsOverlap(a, b){
  return a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;
}

function physics() {
  if (!player.onGround) {
    player.y += -player.vy;      // vy is positive upwards
    player.vy -= S.gravity;
    const groundY = cvs.height - S.groundH - player.h;
    if (player.y >= groundY) {
      player.y = groundY;
      player.vy = 0; 
      player.onGround = true;
      player.bounceOffset = 8; // Add bounce effect when landing
    }
  }
}

function checkCollisions() {
  const pr = { x: player.x, y: player.y, w: player.w, h: player.h };
  
  // Check ground obstacles
  for (const o of obstacles) {
    const or = { x: o.x, y: o.y, w: o.w, h: o.h };
    if (rectsOverlap(pr, or)) { gameOver(); return; }
  }
  
  // Check flying obstacles - collision if player is jumping near them
  for (const f of flyingObstacles) {
    const fr = { x: f.x, y: f.y, w: f.w, h: f.h };
    if (rectsOverlap(pr, fr)) { gameOver(); return; }
  }
}

function drawHud() {
  ctx.fillStyle = 'rgba(0,0,0,.75)';
  ctx.fillRect(0,0,cvs.width, 56);
  ctx.fillStyle = '#fff';
  ctx.font = 'bold 18px Courier New, monospace';
  ctx.fillText(`Score: ${score}`, 16, 34);
  ctx.fillText(`Speed: ${speed.toFixed(1)}`, 180, 34);
  const hs = `High: ${highScore}`;
  ctx.fillText(hs, cvs.width - 16 - ctx.measureText(hs).width, 34);

  // Draw notifications (speed increase popups)
  for (let i = notifications.length - 1; i >= 0; i--) {
    const n = notifications[i];
    const elapsed = Date.now() - n.startTime;
    const progress = elapsed / n.duration;
    
    if (progress >= 1) {
      notifications.splice(i, 1);
      continue;
    }
    
    // Animate scale and fade
    if (progress < 0.2) {
      n.scale = progress * 5; // Scale up quickly
    } else if (progress > 0.8) {
      n.life = (1 - progress) * 5; // Fade out
    }
    
    ctx.save();
    ctx.globalAlpha = n.life;
    ctx.fillStyle = '#FFD700';
    ctx.strokeStyle = '#FF6B35';
    ctx.lineWidth = 3;
    ctx.font = `bold ${Math.floor(24 * n.scale)}px Courier New`;
    ctx.textAlign = 'center';
    
    const x = cvs.width / 2;
    const y = cvs.height / 2 - 50;
    
    ctx.strokeText(n.text, x, y);
    ctx.fillText(n.text, x, y);
    ctx.textAlign = 'start';
    ctx.restore();
  }

  if (paused) {
    ctx.fillStyle = 'rgba(0,0,0,.55)';
    ctx.fillRect(0,0,cvs.width,cvs.height);
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 48px Courier New';
    ctx.textAlign = 'center';
    ctx.fillText('⏸️ PAUSED', cvs.width/2, cvs.height/2 - 8);
    ctx.font = '24px Courier New';
    ctx.fillText('Press SPACE to continue', cvs.width/2, cvs.height/2 + 40);
    ctx.textAlign = 'start';
  }
}

function loop() {
  if (!running) return;
  ctx.clearRect(0,0,cvs.width,cvs.height);
  drawBg();
  if (!paused) {
    physics();
    drawObstacles();
    checkCollisions();
    updateParticles();
  } else {
    // still draw obstacles where they are
    ctx.font = '40px system-ui';
    obstacles.forEach(o => ctx.fillText(o.type, o.x, o.y + o.h));
    flyingObstacles.forEach(f => ctx.fillText(f.type, f.x, f.y + f.h));
  }
  drawPlayer();
  drawParticles();
  drawHud();
  rafId = requestAnimationFrame(loop);
}

function jump() {
  if (!running || paused) return;
  if (player.onGround) {
    player.onGround = false;
    player.vy = S.jumpPower;
  }
}

function showHelpCard(show) {
  const el = document.getElementById("cycloneHelp");
  if (el) el.style.display = show ? 'block' : 'none';
  showHelp = show;
}

function safeSendScore() {
  try { dotnet?.invokeMethodAsync("CycloneScoreUpdate", score, highScore, speed); } catch {}
}

function gameOver() {
  running = false;
  cancelAnimationFrame(rafId);
  if (score > highScore) highScore = score;
  safeSendScore();

  // Show custom game over screen instead of confirm dialog
  gameOverScreen = true;
  showGameOverScreen();
}

function showGameOverScreen() {
  const gameOverEl = document.getElementById("cycloneGameOver");
  const finalScoreEl = document.getElementById("finalScore");
  const highScoreEl = document.getElementById("gameOverHighScore");
  const newHighScoreEl = document.getElementById("newHighScore");
  
  if (gameOverEl && finalScoreEl && highScoreEl) {
    finalScoreEl.textContent = score;
    highScoreEl.textContent = highScore;
    
    // Show new high score message if applicable
    if (newHighScoreEl) {
      newHighScoreEl.style.display = (score > 0 && score === highScore) ? 'block' : 'none';
    }
    
    gameOverEl.style.display = 'flex';
    
    // Add entrance animation
    setTimeout(() => {
      gameOverEl.classList.add('show');
    }, 50);
  }
}

export function exitGame() {
  const gameOverEl = document.getElementById("cycloneGameOver");
  if (gameOverEl) {
    gameOverEl.style.display = 'none';
    gameOverEl.classList.remove('show');
  }
  gameOverScreen = false;
  showHelpCard(true);
  paused = false;
  try { dotnet?.invokeMethodAsync("CyclonePaused", false); } catch {}
}

// exported API

export function initCycloneGame(canvasRef, dotnetRef) {
  cvs = canvasRef; ctx = cvs.getContext('2d');
  dotnet = dotnetRef;

  resize(); addClouds();
  window.addEventListener('resize', resize);

  // keyboard
  cvs.addEventListener('keydown', (e) => {
    if (e.code === 'Space') { e.preventDefault(); if (paused) resumeCycloneGame(); else jump(); }
    else if (e.code === 'Escape') { e.preventDefault(); if (running) togglePause(); }
  });

  // Enhanced mobile touch controls
  cvs.addEventListener('pointerdown', (e)=> {
    e.preventDefault();
    cvs.focus();
    if (!running) startCycloneGame();
    else if (paused) resumeCycloneGame();
    else jump();
  });

  // Additional touch events for better mobile support
  cvs.addEventListener('touchstart', (e) => {
    e.preventDefault();
    cvs.focus();
    if (!running) startCycloneGame();
    else if (paused) resumeCycloneGame();  
    else jump();
  }, { passive: false });

  // Double tap for pause on mobile
  let lastTap = 0;
  cvs.addEventListener('touchend', (e) => {
    const currentTime = Date.now();
    const tapLength = currentTime - lastTap;
    if (tapLength < 300 && tapLength > 0) {
      if (running) togglePause();
    }
    lastTap = currentTime;
  });

  // prevent page scroll on space
  document.addEventListener('keydown', (e)=> {
    if (e.code === 'Space') e.preventDefault();
  }, {capture:true});

  showHelpCard(true);
}

export function startCycloneGame() {
  // Hide game over screen if it's showing
  const gameOverEl = document.getElementById("cycloneGameOver");
  if (gameOverEl) {
    gameOverEl.style.display = 'none';
    gameOverEl.classList.remove('show');
  }
  
  // reset state
  running = true; paused = false; gameOverScreen = false;
  score = 0; speed = S.baseSpeed; 
  speedThreshold = 50; // Reset speed threshold
  obstacles = []; flyingObstacles = []; particles = []; notifications = [];
  nextObstacleDistance = S.obstacleGapMin;
  resize(); addClouds();
  player.y = cvs.height - S.groundH - player.h;
  player.vy = 0; player.onGround = true; player.bounceOffset = 0;

  // spawn first obstacles
  for (let i=0;i<3;i++) spawnObstacle(true);

  showHelpCard(false);
  try { dotnet?.invokeMethodAsync("CyclonePaused", false); } catch {}
  safeSendScore();

  cancelAnimationFrame(rafId);
  rafId = requestAnimationFrame(loop);

  // ensure keyboard focus for space/esc
  setTimeout(()=>cvs.focus(), 0);
}

export function pauseCycloneGame() { if (!running || paused) return; paused = true; try { dotnet?.invokeMethodAsync("CyclonePaused", true); } catch {} }
export function resumeCycloneGame() { if (!running) return; paused = false; try { dotnet?.invokeMethodAsync("CyclonePaused", false); } catch {} }
function togglePause(){ paused ? resumeCycloneGame() : pauseCycloneGame(); }

export function destroyCycloneGame() {
  cancelAnimationFrame(rafId);
  running = false; paused = false;
  // listeners left on window/document are benign, but we keep it minimal
}
