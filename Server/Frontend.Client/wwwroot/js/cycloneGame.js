// Cyclone Escape — Canvas game for Blazor
// Place at: wwwroot/js/cycloneGame.js

let cvs, ctx, dotnet;
let rafId = 0;
let running = false, paused = false, showHelp = true;

let lastTime = 0;
const PX = { 
  baseSpeed: 220,           // ~2.2 px/frame * 100 fps-ish -> tweak
  maxSpeed: 850,            // maps to S.maxSpeed feel
  cloudMin: 40, cloudVar: 70
};

// Convert game speed (S.speed) to pixel speed (PX)
function getPixelSpeed() {
  // Map S.speed (2.2 to 8.5) to PX (220 to 850)
  const ratio = (speed - S.baseSpeed) / (S.maxSpeed - S.baseSpeed);
  return PX.baseSpeed + ratio * (PX.maxSpeed - PX.baseSpeed);
}


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
let speedThreshold = 75; // Track next speed increase threshold - slower progression
let obstacles = [];
let flyingObstacles = [];
let powerUps = []; // New power-up system
let clouds = [];
let particles = [];
let notifications = [];
let nextObstacleDistance = 0;
let gameOverScreen = false;
let difficultyPattern = 'normal'; // Track current spacing pattern
let patternChangeCounter = 0; // Counter for pattern changes
let day = 0; // 0..1 for day/night cycle
let playerEffects = { // Player power-up effects
  shield: { active: false, duration: 0, particles: [] },
  multiplier: { active: false, duration: 0, value: 1 },
  invincible: { active: false, duration: 0 }
};

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
      sp: PX.cloudMin + Math.random() * PX.cloudVar // px/s
    });
  }
}


function spawnObstacle(force=false) {
  if (!force && obstacles.length) {
    const last = obstacles[obstacles.length - 1];
    if (last.x > cvs.width - nextObstacleDistance) return;
  }
  
  // Variable difficulty patterns - change every 5-8 obstacles
  patternChangeCounter++;
  if (patternChangeCounter >= (5 + Math.floor(Math.random() * 4))) {
    patternChangeCounter = 0;
    const patterns = ['tight', 'normal', 'wide', 'empty'];
    difficultyPattern = patterns[Math.floor(Math.random() * patterns.length)];
  }
  
  // Base gap calculation
  const baseGap = S.obstacleGapMin + Math.random() * S.obstacleGapVar;
  const speedRatio = S.baseSpeed / speed;
  const speedScale = Math.max(0.75, Math.min(1.0, speedRatio));
  
  // Apply pattern-based spacing
  let patternMultiplier = 1.0;
  let skipSpawn = false;
  
  switch(difficultyPattern) {
    case 'tight': 
      patternMultiplier = 0.6 + Math.random() * 0.3; // 60-90% spacing
      break;
    case 'normal': 
      patternMultiplier = 0.8 + Math.random() * 0.4; // 80-120% spacing
      break;
    case 'wide': 
      patternMultiplier = 1.2 + Math.random() * 0.6; // 120-180% spacing
      break;
    case 'empty': 
      // 30% chance to skip spawning for empty passages
      if (Math.random() < 0.3) {
        skipSpawn = true;
        nextObstacleDistance = baseGap * 2.0; // Large gap
      } else {
        patternMultiplier = 1.0 + Math.random() * 0.5; // Normal to wide
      }
      break;
  }
  
  if (skipSpawn) return;
  
  nextObstacleDistance = baseGap * speedScale * patternMultiplier;
  
  // Dynamic minimum gap based on speed
  const speedBasedMinGap = 180 + (speed - S.baseSpeed) * 20;
  nextObstacleDistance = Math.max(speedBasedMinGap, nextObstacleDistance);
  
  const types = ['🌳','🪨','🌵','🚧','🚗'];
  const newObstacle = {
    x: cvs.width + 20,
    y: cvs.height - S.groundH - 50,
    w: 40, h: 50,
    type: types[Math.floor(Math.random()*types.length)],
    spawnAnim: 1.0 // Animation scale for spawn effect
  };
  
  obstacles.push(newObstacle);
  
  // Improved flying obstacle spawning - better chances and timing
  const baseChance = 0.15 + (speed - S.baseSpeed) * 0.02; // Increased base chance
  const flyingChance = Math.min(0.35, baseChance); // Higher max chance
  
  // More aggressive flying spawning in tight patterns, less in wide/empty
  const patternBonus = difficultyPattern === 'tight' ? 0.1 : 
                      difficultyPattern === 'wide' || difficultyPattern === 'empty' ? -0.05 : 0;
                      
  if (Math.random() < (flyingChance + patternBonus) && score > 30) { // Lower score requirement
    const delay = Math.random() * 1200 + 600; // Shorter delays
    setTimeout(() => spawnFlyingObstacle(), delay);
  }
  
  // Spawn power-ups occasionally
  if (Math.random() < 0.08 && score > 100) { // 8% chance after score 100
    spawnPowerUp();
  }
}

function spawnFlyingObstacle() {
  const flyingTypes = ['🦅','🦇','🐦'];
  
  // Calculate player's jump height using proper physics
  // Jump peak height = initial velocity² / (2 * gravity)
  const calculatedHeight = S.jumpPower * S.jumpPower / (2 * S.gravity);
  const jumpPeakHeight = cvs.height - S.groundH - player.h - calculatedHeight;
  
  // Ensure flying obstacles appear in valid range
  const minY = 80; // Safe distance from top
  const maxY = cvs.height - S.groundH - 80; // Safe distance from ground
  const clampedY = Math.max(minY, Math.min(maxY, jumpPeakHeight));
  
  // Reduced conflict checking - only check immediate area for more flying obstacles
  const flyingX = cvs.width + 50;
  const conflictZone = 200; // Reduced from 300px to 200px for less restrictive spawning
  
  let hasConflict = false;
  for (const obstacle of obstacles) {
    // Only prevent if obstacle is very close to flying spawn point
    const obstacleEndX = obstacle.x + obstacle.w;
    const flyingEndX = flyingX + 35; // flying obstacle width
    
    // More lenient conflict detection - only block if very close overlap
    if (obstacle.x < flyingX + conflictZone && obstacleEndX > flyingX - 100) {
      hasConflict = true;
      break;
    }
  }
  
  // Also check against other flying obstacles to prevent crowding
  for (const flying of flyingObstacles) {
    if (Math.abs(flying.x - flyingX) < 150) { // Don't spawn too close to other flying
      hasConflict = true;
      break;
    }
  }
  
  // More permissive spawning conditions
  if (!hasConflict && clampedY > 0 && clampedY < cvs.height - 100) {
    flyingObstacles.push({
      x: flyingX,
      y: clampedY,
      w: 35, h: 25,
      type: flyingTypes[Math.floor(Math.random()*flyingTypes.length)],
      warning: true, // Show warning indicator
      spawnAnim: 1.0
    });
  }
}

function spawnPowerUp() {
  const powerUpTypes = [
    { emoji: '🛡️', type: 'shield', name: 'Shield' },
    { emoji: '⭐', type: 'multiplier', name: '2x Points' },
    { emoji: '💎', type: 'invincible', name: 'Invincible' }
  ];
  
  const powerUpType = powerUpTypes[Math.floor(Math.random() * powerUpTypes.length)];
  
  powerUps.push({
    x: cvs.width + 30,
    y: cvs.height - S.groundH - 60,
    w: 30, h: 30,
    emoji: powerUpType.emoji,
    type: powerUpType.type,
    name: powerUpType.name,
    spawnAnim: 1.0,
    bobOffset: 0,
    collectEffect: false
  });
}

function updatePowerUps(dt) {
  // Update power-up positions and animations
  for (let i = powerUps.length - 1; i >= 0; i--) {
    const p = powerUps[i];
    
    // Use PX system for consistent movement
    p.x -= getPixelSpeed() * dt;
    p.bobOffset += dt * 8; // Floating animation
    
    // Animate spawn effect
    if (p.spawnAnim > 0) {
      p.spawnAnim -= 0.03;
    }
    
    // Remove if off-screen
    if (p.x + p.w < 0) {
      powerUps.splice(i, 1);
      continue;
    }
    
    // Check collection
    if (rectsOverlap(player, p)) {
      collectPowerUp(p);
      powerUps.splice(i, 1);
    }
  }
  
  // Update player effects
  updatePlayerEffects(dt);
}

function collectPowerUp(powerUp) {
  // Visual feedback
  createParticles(powerUp.x + 15, powerUp.y + 15, 20, 'powerup');
  showNotification(`${powerUp.emoji} ${powerUp.name}!`, 1000);
  
  // Apply effect
  switch(powerUp.type) {
    case 'shield':
      playerEffects.shield.active = true;
      playerEffects.shield.duration = 8000; // 8 seconds
      break;
    case 'multiplier':
      playerEffects.multiplier.active = true;
      playerEffects.multiplier.duration = 10000; // 10 seconds
      playerEffects.multiplier.value = 2;
      break;
    case 'invincible':
      playerEffects.invincible.active = true;
      playerEffects.invincible.duration = 6000; // 6 seconds
      break;
  }
}

function updatePlayerEffects(dt) {
  const dtMs = dt * 1000;
  
  // Update shield
  if (playerEffects.shield.active) {
    playerEffects.shield.duration -= dtMs;
    if (playerEffects.shield.duration <= 0) {
      playerEffects.shield.active = false;
    }
  }
  
  // Update multiplier
  if (playerEffects.multiplier.active) {
    playerEffects.multiplier.duration -= dtMs;
    if (playerEffects.multiplier.duration <= 0) {
      playerEffects.multiplier.active = false;
      playerEffects.multiplier.value = 1;
    }
  }
  
  // Update invincible
  if (playerEffects.invincible.active) {
    playerEffects.invincible.duration -= dtMs;
    if (playerEffects.invincible.duration <= 0) {
      playerEffects.invincible.active = false;
    }
  }
}

function drawPowerUps() {
  ctx.font = '30px system-ui';
  
  powerUps.forEach(p => {
    ctx.save();
    
    // Floating animation
    const bobY = p.y + Math.sin(p.bobOffset) * 5;
    
    // Spawn animation
    if (p.spawnAnim > 0) {
      const scale = Math.max(0.1, 1 - p.spawnAnim);
      ctx.scale(scale, scale);
      ctx.translate(p.x / scale, bobY / scale);
    } else {
      ctx.translate(p.x, bobY);
    }
    
    // Glow effect
    ctx.shadowColor = '#FFD700';
    ctx.shadowBlur = 10;
    ctx.fillText(p.emoji, 0, 0);
    
    ctx.restore();
  });
}

function drawBg(dt = 0) {
  day = (day + dt * 0.02) % 1; // 50s full cycle

  // sky gradient shifts from day to night
  const top = day < 0.5 ? '#87CEEB' : '#1a2a6c';
  const mid = day < 0.5 ? '#98FB98' : '#203a43';
  const bot = day < 0.5 ? '#8FBC8F' : '#2c5364';
  const g = ctx.createLinearGradient(0, 0, 0, cvs.height);
  g.addColorStop(0, top); g.addColorStop(0.7, mid); g.addColorStop(1, bot);
  ctx.fillStyle = g; ctx.fillRect(0, 0, cvs.width, cvs.height);

  // parallax hills - back to front for proper layering
  drawHill(0.15, 120, dt, 0); // Far hills - slower, taller, brown
  drawHill(0.3, 80, dt, 1);   // Near hills - faster, shorter, green

  // clouds
  ctx.font = '24px system-ui';
  clouds.forEach(c => {
    ctx.fillText('☁️', c.x, c.y);
    c.x -= c.sp * dt;
    if (c.x < -60) { c.x = cvs.width + 40; c.y = 50 + Math.random() * 180; }
  });

  // ground - make sure it's visible
  ctx.fillStyle = day < 0.5 ? '#228B22' : '#1a4a1a'; // Darker green at night
  ctx.fillRect(0, cvs.height - S.groundH, cvs.width, S.groundH);
  ctx.fillStyle = day < 0.5 ? '#8B4513' : '#654321'; // Darker brown at night
  ctx.fillRect(0, cvs.height - S.groundH, cvs.width, 3);
}

function drawHill(speedFactor, height, dt, layer = 0) {
  // Different hill colors for variation and day/night
  let hillColor;
  if (layer === 0) {
    // Back layer - darker/brown hills
    hillColor = day < 0.5 ? 'rgba(139, 69, 19, 0.4)' : 'rgba(69, 39, 19, 0.5)';
  } else {
    // Front layer - green hills
    hillColor = day < 0.5 ? 'rgba(34, 139, 34, 0.6)' : 'rgba(20, 60, 40, 0.7)';
  }
  
  ctx.fillStyle = hillColor;
  
  // Hill generation system with continuous spawning
  const step = 200; // Width of each hill segment
  const hillSpeed = getPixelSpeed() * speedFactor;
  
  // Initialize hill system if needed
  if (!drawHill.hillData) {
    drawHill.hillData = {};
  }
  if (!drawHill.hillData[layer]) {
    drawHill.hillData[layer] = {
      offset: 0,
      hills: [],
      nextHillId: 0
    };
  }
  
  const hillSystem = drawHill.hillData[layer];
  
  // Move hills continuously
  hillSystem.offset -= hillSpeed * dt;
  
  // Generate new hills ahead of the viewport
  const viewportLeft = hillSystem.offset;
  const viewportRight = hillSystem.offset + cvs.width;
  const generationAhead = cvs.width; // Generate hills this far ahead
  
  // Remove hills that are too far behind
  hillSystem.hills = hillSystem.hills.filter(hill => hill.x + step > viewportLeft - step);
  
  // Generate new hills if needed
  let rightmostHill = hillSystem.hills.length > 0 ? 
    Math.max(...hillSystem.hills.map(h => h.x + step)) : viewportLeft;
  
  while (rightmostHill < viewportRight + generationAhead) {
    const newHill = {
      id: hillSystem.nextHillId++,
      x: rightmostHill,
      heightVariation: (Math.random() - 0.5) * 40, // ±20 pixel variation
      shapeVariation: Math.random() * 2 - 1, // -1 to 1 for shape variety
      peakOffset: (Math.random() - 0.5) * 0.3 // Offset peak position
    };
    hillSystem.hills.push(newHill);
    rightmostHill += step;
  }
  
  // Draw all visible hills
  for (const hill of hillSystem.hills) {
    const screenX = hill.x - hillSystem.offset;
    
    // Skip hills completely outside viewport
    if (screenX + step < 0 || screenX > cvs.width) continue;
    
    // Calculate hill properties
    const actualHeight = height + hill.heightVariation;
    const peakX = screenX + step * (0.5 + hill.peakOffset * 0.3);
    
    // Create hill shape with curves
    ctx.beginPath();
    ctx.moveTo(screenX, cvs.height - S.groundH);
    
    // Multiple curves for more natural hills with variation
    const midPoint1 = screenX + step * (0.3 + hill.shapeVariation * 0.1);
    const midPoint2 = screenX + step * (0.7 - hill.shapeVariation * 0.1);
    const heightMod1 = 0.7 + hill.shapeVariation * 0.2;
    const heightMod2 = 0.7 - hill.shapeVariation * 0.1;
    
    ctx.quadraticCurveTo(
      midPoint1, 
      cvs.height - S.groundH - actualHeight * heightMod1, 
      peakX, 
      cvs.height - S.groundH - actualHeight
    );
    ctx.quadraticCurveTo(
      midPoint2, 
      cvs.height - S.groundH - actualHeight * heightMod2, 
      screenX + step, 
      cvs.height - S.groundH
    );
    
    ctx.lineTo(screenX + step, cvs.height);
    ctx.lineTo(screenX, cvs.height);
    ctx.closePath();
    ctx.fill();
    
    // Add gradient effect for depth (glazing)
    if (layer === 1) {
      const gradient = ctx.createLinearGradient(0, cvs.height - S.groundH - actualHeight, 0, cvs.height - S.groundH);
      gradient.addColorStop(0, 'rgba(255, 255, 255, 0.1)');
      gradient.addColorStop(1, 'rgba(255, 255, 255, 0)');
      ctx.fillStyle = gradient;
      ctx.fill();
      ctx.fillStyle = hillColor; // Reset for next hill
    }
  }
}

function drawPlayer() {
  ctx.save();
  
  // Add bounce animation when landing
  if (player.onGround && player.bounceOffset > 0) {
    player.bounceOffset *= 0.85; // Smooth decay
    if (player.bounceOffset < 0.5) player.bounceOffset = 0;
  }
  
  // Power-up visual effects
  if (playerEffects.shield.active) {
    // Shield bubble effect
    ctx.beginPath();
    ctx.arc(player.x + player.w/2, player.y + player.h/2 - player.bounceOffset, 35, 0, Math.PI * 2);
    ctx.strokeStyle = 'rgba(0, 191, 255, 0.6)';
    ctx.lineWidth = 3;
    ctx.stroke();
    
    // Sparkle particles around shield
    for(let i = 0; i < 3; i++) {
      const angle = Date.now() * 0.01 + i * Math.PI * 2 / 3;
      const x = player.x + player.w/2 + Math.cos(angle) * 30;
      const y = player.y + player.h/2 + Math.sin(angle) * 30;
      ctx.fillStyle = 'rgba(0, 191, 255, 0.8)';
      ctx.fillRect(x-2, y-2, 4, 4);
    }
  }
  
  if (playerEffects.invincible.active) {
    // Rainbow glow effect
    const time = Date.now() * 0.01;
    ctx.shadowColor = `hsl(${time * 50 % 360}, 100%, 50%)`;
    ctx.shadowBlur = 15;
  }
  
  ctx.scale(-1, 1); // Flip horizontally to face right
  ctx.font = '40px system-ui';
  
  // Flashing effect for invincible
  if (playerEffects.invincible.active && Math.floor(Date.now() / 100) % 2) {
    ctx.globalAlpha = 0.5;
  }
  
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

function updateParticles(dt) {
  for (let i = particles.length - 1; i >= 0; i--) {
    const p = particles[i];
    p.x += p.vx * dt * 60; // Scale for consistent speed
    p.y += p.vy * dt * 60;
    p.life -= p.decay;
    p.vy += 0.1 * dt * 60; // Gravity
    
    if (p.life <= 0) {
      particles.splice(i, 1);
    }
  }
}

function drawParticles() {
  particles.forEach(p => {
    ctx.save();
    ctx.globalAlpha = p.life;
    
    if (p.type === 'speed') {
      ctx.fillStyle = '#FFD700';
    } else if (p.type === 'powerup') {
      // Rainbow particles for power-ups
      const hue = (Date.now() * 0.1 + p.x + p.y) % 360;
      ctx.fillStyle = `hsl(${hue}, 100%, 60%)`;
    } else {
      ctx.fillStyle = '#FF6B35';
    }
    
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

function drawObstacles(dt) {
  ctx.font = '40px system-ui';
  
  const pixelSpeed = getPixelSpeed();
  
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
    
    // Use PX system for consistent movement
    o.x -= pixelSpeed * dt;
    if (o.x + o.w < 0) {
      obstacles.splice(i,1);
      
      // Apply score multiplier from power-ups
      const basePoints = 10;
      const multiplier = playerEffects.multiplier.active ? playerEffects.multiplier.value : 1;
      score += basePoints * multiplier;
      
      // Show multiplier effect
      if (multiplier > 1) {
        showNotification(`+${basePoints * multiplier} (${multiplier}x)`, 800);
      }
      
      // Slower speed ramping with visual feedback - more time to adapt
      if (score >= speedThreshold && speed < S.maxSpeed) {
        const oldSpeed = speed;
        speed = Math.min(S.maxSpeed, speed + 0.4); // Reduced from 0.6 to 0.4
        speedThreshold += 75; // Increased from 50 to 75 - slower progression
        
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
    
    // Validate flying obstacle position - remove if off-screen or invalid
    if (f.y < 0 || f.y > cvs.height - 50) {
      flyingObstacles.splice(i, 1);
      continue;
    }
    
    // Show warning indicator - more accurate timing for straight-flying obstacles
    if (f.warning && f.x > cvs.width * 0.75 && f.x < cvs.width * 1.1) {
      ctx.save();
      ctx.fillStyle = 'rgba(255, 0, 0, 0.8)';
      ctx.font = 'bold 18px Arial';
      ctx.textAlign = 'center';
      const warningY = Math.max(90, f.y - 30); // Ensure warning doesn't go off-screen
      ctx.fillText('⚠️ DON\'T JUMP! ⚠️', cvs.width / 2, warningY);
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
    
    // Use PX system for flying obstacles
    f.x -= pixelSpeed * dt;
    if (f.x + f.w < 0) {
      flyingObstacles.splice(i,1);
      
      // Apply score multiplier for flying obstacles
      const basePoints = 15;
      const multiplier = playerEffects.multiplier.active ? playerEffects.multiplier.value : 1;
      score += basePoints * multiplier; // More points for avoiding flying obstacles
      
      if (multiplier > 1) {
        showNotification(`+${basePoints * multiplier} (${multiplier}x)`, 800);
      }
      
      safeSendScore();
    }
  }
  
  spawnObstacle();
}

function rectsOverlap(a, b){
  return a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;
}

function physics(dt) {
  if (!player.onGround) {
    player.y += -player.vy * dt * 60;      // keep your jump tuning
    player.vy -= S.gravity * dt * 60;

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
  // Skip collision if invincible
  if (playerEffects.invincible.active) return;
  
  // Use smaller hitboxes for more forgiving gameplay (75% of visual size)
  const hitboxScale = 0.75;
  const playerOffset = (1 - hitboxScale) / 2;
  
  const pr = { 
    x: player.x + (player.w * playerOffset), 
    y: player.y + (player.h * playerOffset), 
    w: player.w * hitboxScale, 
    h: player.h * hitboxScale 
  };
  
  // Check ground obstacles with smaller hitboxes
  for (const o of obstacles) {
    const obstacleOffset = (1 - hitboxScale) / 2;
    const or = { 
      x: o.x + (o.w * obstacleOffset), 
      y: o.y + (o.h * obstacleOffset), 
      w: o.w * hitboxScale, 
      h: o.h * hitboxScale 
    };
    if (rectsOverlap(pr, or)) { 
      if (playerEffects.shield.active) {
        // Shield absorbs hit
        playerEffects.shield.active = false;
        showNotification('🛡️ Shield Protected!', 1000);
        createParticles(player.x + 20, player.y + 20, 10, 'powerup');
        return;
      }
      gameOver(); 
      return; 
    }
  }
  
  // Check flying obstacles with smaller hitboxes
  for (const f of flyingObstacles) {
    const flyingOffset = (1 - hitboxScale) / 2;
    const fr = { 
      x: f.x + (f.w * flyingOffset), 
      y: f.y + (f.h * flyingOffset), 
      w: f.w * hitboxScale, 
      h: f.h * hitboxScale 
    };
    if (rectsOverlap(pr, fr)) { 
      if (playerEffects.shield.active) {
        // Shield absorbs hit
        playerEffects.shield.active = false;
        showNotification('🛡️ Shield Protected!', 1000);
        createParticles(player.x + 20, player.y + 20, 10, 'powerup');
        return;
      }
      gameOver(); 
      return; 
    }
  }
}

function drawHud() {
  ctx.fillStyle = 'rgba(0,0,0,.75)';
  ctx.fillRect(0,0,cvs.width, 56);
  ctx.fillStyle = '#fff';
  ctx.font = 'bold 18px Courier New, monospace';
  ctx.fillText(`Score: ${score}`, 16, 34);
  ctx.fillText(`Speed: ${speed.toFixed(1)}`, 180, 34);
  
  // Show active power-ups
  let powerUpX = 520;
  if (playerEffects.shield.active) {
    ctx.fillStyle = '#00BFFF';
    ctx.fillText(`🛡️ ${Math.ceil(playerEffects.shield.duration/1000)}s`, powerUpX, 34);
    powerUpX += 80;
  }
  if (playerEffects.multiplier.active) {
    ctx.fillStyle = '#FFD700';
    ctx.fillText(`⭐ ${playerEffects.multiplier.value}x ${Math.ceil(playerEffects.multiplier.duration/1000)}s`, powerUpX, 34);
    powerUpX += 100;
  }
  if (playerEffects.invincible.active) {
    ctx.fillStyle = '#FF69B4';
    ctx.fillText(`💎 ${Math.ceil(playerEffects.invincible.duration/1000)}s`, powerUpX, 34);
  }
  
  ctx.fillStyle = '#fff';
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

function loop(t) {
  if (!running) return;
  if (!lastTime) lastTime = t;
  const dt = Math.min(0.033, (t - lastTime) / 1000); // clamp at 33ms
  lastTime = t;

  ctx.clearRect(0,0,cvs.width,cvs.height);
  drawBg(dt);
  if (!paused) {
    physics(dt);
    drawObstacles(dt);
    updatePowerUps(dt);
    checkCollisions();
    updateParticles(dt);
  } else {
    ctx.font = '40px system-ui';
    obstacles.forEach(o => ctx.fillText(o.type, o.x, o.y + o.h));
    flyingObstacles.forEach(f => ctx.fillText(f.type, f.x, f.y + f.h));
    powerUps.forEach(p => ctx.fillText(p.emoji, p.x, p.y + p.h));
  }
  drawPlayer();
  drawPowerUps();
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
  speedThreshold = 75; // Reset speed threshold - slower progression
  obstacles = []; flyingObstacles = []; powerUps = []; particles = []; notifications = [];
  nextObstacleDistance = S.obstacleGapMin;
  difficultyPattern = 'normal'; // Reset pattern
  patternChangeCounter = 0; // Reset pattern counter
  day = 0; // Reset day/night cycle
  
  // Reset hill system for clean start
  if (drawHill.hillData) {
    drawHill.hillData = {};
  }
  
  // Reset all player effects
  playerEffects.shield.active = false;
  playerEffects.shield.duration = 0;
  playerEffects.multiplier.active = false;
  playerEffects.multiplier.duration = 0;
  playerEffects.multiplier.value = 1;
  playerEffects.invincible.active = false;
  playerEffects.invincible.duration = 0;
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
