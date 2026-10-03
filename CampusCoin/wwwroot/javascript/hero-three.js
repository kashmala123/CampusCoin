/* ════════════════════════════════════════════════════════════
   CAMPUS COIN — HERO 3D SCENE v25
   Clean planet · no rings · 4 cards in corners
   ════════════════════════════════════════════════════════════ */

(async function initHero() {
    const canvas = document.getElementById('ccHeroCanvas');
    if (!canvas) return;
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

    let THREE;
    try {
        THREE = await import('https://unpkg.com/three@0.160.0/build/three.module.js');
    } catch (err) { console.warn(err); return; }

    const renderer = new THREE.WebGLRenderer({
        canvas, alpha: true, antialias: true,
        powerPreference: 'high-performance'
    });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.toneMapping = THREE.NoToneMapping;
    renderer.outputColorSpace = THREE.SRGBColorSpace;

    const scene  = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(28, 1, 0.1, 100);
    camera.position.set(0, 0, 9);

    /* ---------- Lights ---------- */
    scene.add(new THREE.AmbientLight(0xffffff, 0.7));

    const sun = new THREE.DirectionalLight(0xffffff, 1.9);
    sun.position.set(8, 6, 9);
    scene.add(sun);

    const fill = new THREE.DirectionalLight(0xc4aee0, 0.35);
    fill.position.set(-6, 2, -6);
    scene.add(fill);

    const coinLight = new THREE.DirectionalLight(0xfff4d8, 1.6);
    coinLight.position.set(3, 5, 8);
    scene.add(coinLight);

    const rimLight = new THREE.DirectionalLight(0xffe8a0, 1.2);
    rimLight.position.set(-5, 3, 2);
    scene.add(rimLight);

    /* ---------- Pedestal ---------- */
    const ped = new THREE.Mesh(
        new THREE.CylinderGeometry(1.85, 1.85, 0.14, 80),
        new THREE.MeshPhongMaterial({
            color: 0xede4f6, shininess: 70, specular: 0xffffff
        })
    );
    ped.position.y = -1.8;
    scene.add(ped);

    const shadow = new THREE.Mesh(
        new THREE.CircleGeometry(1.8, 64),
        new THREE.MeshBasicMaterial({ color: 0x5533a0, transparent: true, opacity: 0.2 })
    );
    shadow.rotation.x = -Math.PI / 2;
    shadow.position.y = -1.87;
    scene.add(shadow);

    const orbGroup = new THREE.Group();
    scene.add(orbGroup);

    /* ============================================================
       CONTINENT HELPERS
       ============================================================ */
    function generateContinent(cx, cy, baseR) {
        const points = [];
        const n = 24 + Math.floor(Math.random() * 10);
        const seedA = Math.random() * 6.28;
        const seedB = Math.random() * 6.28;
        for (let i = 0; i < n; i++) {
            const angle = (i / n) * Math.PI * 2;
            const noise = 0.72
                + Math.sin(angle * 2.3 + seedA) * 0.14
                + Math.sin(angle * 4.7 + seedB) * 0.09
                + Math.sin(angle * 7.1 + seedA * 2) * 0.05;
            const r = baseR * noise;
            points.push([
                cx + Math.cos(angle) * r * 1.35,
                cy + Math.sin(angle) * r
            ]);
        }
        return points;
    }

    function tracePath(ctx, points) {
        ctx.beginPath();
        ctx.moveTo(points[0][0], points[0][1]);
        for (let i = 1; i < points.length; i++) {
            const p = points[i];
            const prev = points[i - 1];
            const midX = (prev[0] + p[0]) / 2;
            const midY = (prev[1] + p[1]) / 2;
            ctx.quadraticCurveTo(prev[0], prev[1], midX, midY);
        }
        const last = points[points.length - 1];
        const first = points[0];
        ctx.quadraticCurveTo(last[0], last[1], (last[0] + first[0]) / 2, (last[1] + first[1]) / 2);
        ctx.quadraticCurveTo(first[0], first[1], first[0], first[1]);
        ctx.closePath();
    }

    /* ============================================================
       PLANET TEXTURE
       ============================================================ */
    function makePlanetTexture() {
        const c = document.createElement('canvas');
        c.width = 2048; c.height = 1024;
        const ctx = c.getContext('2d');

        const bg = ctx.createLinearGradient(0, 0, 0, 1024);
        bg.addColorStop(0.0, '#4b2568');
        bg.addColorStop(0.35, '#5c2f7e');
        bg.addColorStop(0.65, '#6b3a92');
        bg.addColorStop(1.0, '#4b2568');
        ctx.fillStyle = bg;
        ctx.fillRect(0, 0, 2048, 1024);

        for (let i = 0; i < 100; i++) {
            const x = Math.random() * 2048;
            const y = Math.random() * 1024;
            const r = 200 + Math.random() * 400;
            const g2 = ctx.createRadialGradient(x, y, 0, x, y, r);
            if (Math.random() > 0.5) {
                g2.addColorStop(0, 'rgba(139,88,184,.4)');
                g2.addColorStop(1, 'rgba(139,88,184,0)');
            } else {
                g2.addColorStop(0, 'rgba(45,20,80,.4)');
                g2.addColorStop(1, 'rgba(45,20,80,0)');
            }
            ctx.fillStyle = g2;
            ctx.fillRect(x - r, y - r, r * 2, r * 2);
        }

        const continents = [
            { pos: [340, 260], size: 145, color: '#d8c0f0', outline: '#3a1d56' },
            { pos: [500, 640], size: 130, color: '#a8e8c8', outline: '#0a5b3a' },
            { pos: [1050, 320], size: 160, color: '#f5c8d8', outline: '#7a2d42' },
            { pos: [1120, 700], size: 150, color: '#d0b8ec', outline: '#3a1d56' },
            { pos: [1620, 380], size: 190, color: '#d8c0f0', outline: '#3a1d56' },
            { pos: [1440, 560], size: 90,  color: '#f5c8d8', outline: '#7a2d42' },
            { pos: [1740, 800], size: 120, color: '#f5e0b8', outline: '#7a5a10' },
            { pos: [540, 130], size: 75,  color: '#ede4f6', outline: '#5c3d7a' },
            { pos: [1200, 990], size: 175, color: '#e8ddf5', outline: '#5c3d7a' }
        ];

        continents.forEach(cont => {
            const points = generateContinent(cont.pos[0], cont.pos[1], cont.size);

            ctx.save();
            ctx.shadowColor = 'rgba(20,8,40,.85)';
            ctx.shadowBlur = 25;
            ctx.shadowOffsetX = 6;
            ctx.shadowOffsetY = 9;
            tracePath(ctx, points);
            ctx.fillStyle = cont.outline;
            ctx.fill();
            ctx.restore();

            tracePath(ctx, points);
            ctx.fillStyle = cont.color;
            ctx.fill();

            ctx.strokeStyle = cont.outline;
            ctx.lineWidth = 4;
            tracePath(ctx, points);
            ctx.stroke();

            ctx.save();
            tracePath(ctx, points);
            ctx.clip();
            const grd = ctx.createRadialGradient(
                cont.pos[0] - cont.size * 0.4, cont.pos[1] - cont.size * 0.4, 0,
                cont.pos[0], cont.pos[1], cont.size * 1.4
            );
            grd.addColorStop(0, 'rgba(255,255,255,.55)');
            grd.addColorStop(0.5, 'rgba(255,255,255,.08)');
            grd.addColorStop(1, 'rgba(255,255,255,0)');
            ctx.fillStyle = grd;
            ctx.fillRect(cont.pos[0] - cont.size * 2, cont.pos[1] - cont.size * 2, cont.size * 4, cont.size * 4);
            ctx.restore();

            ctx.save();
            tracePath(ctx, points);
            ctx.clip();
            const darkG = ctx.createRadialGradient(
                cont.pos[0] + cont.size * 0.5, cont.pos[1] + cont.size * 0.5, 0,
                cont.pos[0] + cont.size * 0.5, cont.pos[1] + cont.size * 0.5, cont.size * 1.2
            );
            darkG.addColorStop(0, 'rgba(30,15,60,.35)');
            darkG.addColorStop(1, 'rgba(30,15,60,0)');
            ctx.fillStyle = darkG;
            ctx.fillRect(cont.pos[0] - cont.size * 2, cont.pos[1] - cont.size * 2, cont.size * 4, cont.size * 4);
            ctx.restore();

            for (let i = 0; i < 7; i++) {
                const angle = Math.random() * Math.PI * 2;
                const dist = cont.size * (1.7 + Math.random() * 0.8);
                const ix = cont.pos[0] + Math.cos(angle) * dist;
                const iy = cont.pos[1] + Math.sin(angle) * dist;
                const ir = cont.size * (0.08 + Math.random() * 0.15);
                const iPoints = generateContinent(ix, iy, ir);

                ctx.save();
                ctx.shadowColor = 'rgba(20,8,40,.6)';
                ctx.shadowBlur = 10;
                ctx.shadowOffsetX = 3;
                ctx.shadowOffsetY = 4;
                tracePath(ctx, iPoints);
                ctx.fillStyle = cont.outline;
                ctx.fill();
                ctx.restore();

                tracePath(ctx, iPoints);
                ctx.fillStyle = cont.color;
                ctx.fill();

                ctx.strokeStyle = cont.outline;
                ctx.lineWidth = 2;
                tracePath(ctx, iPoints);
                ctx.stroke();
            }
        });

        const t = new THREE.CanvasTexture(c);
        t.colorSpace = THREE.SRGBColorSpace;
        t.anisotropy = 16;
        return t;
    }

    const planet = new THREE.Mesh(
        new THREE.SphereGeometry(1.35, 128, 128),
        new THREE.MeshPhongMaterial({
            map: makePlanetTexture(),
            shininess: 15,
            specular: 0x404060,
            emissive: 0x3a1d56,
            emissiveIntensity: 0.1
        })
    );
    orbGroup.add(planet);

    /* ---------- City lights ---------- */
    const cityGeo = new THREE.BufferGeometry();
    const cityCount = 180;
    const cityPositions = new Float32Array(cityCount * 3);
    for (let i = 0; i < cityCount; i++) {
        const theta = Math.random() * Math.PI * 2;
        const phi = Math.acos(2 * Math.random() - 1);
        const r = 1.36;
        cityPositions[i * 3]     = r * Math.sin(phi) * Math.cos(theta);
        cityPositions[i * 3 + 1] = r * Math.cos(phi);
        cityPositions[i * 3 + 2] = r * Math.sin(phi) * Math.sin(theta);
    }
    cityGeo.setAttribute('position', new THREE.BufferAttribute(cityPositions, 3));

    const cityPoints = new THREE.Points(cityGeo, new THREE.PointsMaterial({
        color: 0xffe8a8,
        size: 0.022,
        sizeAttenuation: true,
        transparent: true,
        opacity: 0.7,
        blending: THREE.AdditiveBlending,
        depthWrite: false,
        toneMapped: false
    }));
    orbGroup.add(cityPoints);

    /* ============================================================
       ATMOSPHERE
       ============================================================ */
    const innerAtmo = new THREE.Mesh(
        new THREE.SphereGeometry(1.55, 96, 96),
        new THREE.ShaderMaterial({
            uniforms: {
                glowColor: { value: new THREE.Color(0xe8ddf5) },
                intensity: { value: 1.6 },
                power: { value: 3.2 }
            },
            vertexShader: `
                varying vec3 vNormal;
                varying vec3 vPosition;
                void main() {
                    vNormal = normalize(normalMatrix * normal);
                    vPosition = (modelViewMatrix * vec4(position, 1.0)).xyz;
                    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
                }
            `,
            fragmentShader: `
                uniform vec3 glowColor;
                uniform float intensity;
                uniform float power;
                varying vec3 vNormal;
                varying vec3 vPosition;
                void main() {
                    vec3 viewDir = normalize(-vPosition);
                    float fresnel = pow(1.0 - abs(dot(vNormal, viewDir)), power);
                    gl_FragColor = vec4(glowColor, fresnel * intensity);
                }
            `,
            blending: THREE.AdditiveBlending,
            transparent: true,
            side: THREE.BackSide,
            depthWrite: false
        })
    );
    orbGroup.add(innerAtmo);

    const midGlow = new THREE.Mesh(
        new THREE.SphereGeometry(1.75, 64, 64),
        new THREE.MeshBasicMaterial({
            color: 0xc4aee0,
            transparent: true,
            opacity: 0.09,
            side: THREE.BackSide,
            blending: THREE.AdditiveBlending,
            depthWrite: false
        })
    );
    orbGroup.add(midGlow);

    const outerGlow = new THREE.Mesh(
        new THREE.SphereGeometry(2.05, 48, 48),
        new THREE.MeshBasicMaterial({
            color: 0xd4b8ff,
            transparent: true,
            opacity: 0.05,
            side: THREE.BackSide,
            blending: THREE.AdditiveBlending,
            depthWrite: false
        })
    );
    orbGroup.add(outerGlow);

    /* ============================================================
       CHUNKY 3D COINS
       ============================================================ */
    function makeCoinFaceTexture() {
        const c = document.createElement('canvas');
        c.width = 512; c.height = 512;
        const ctx = c.getContext('2d');
        const cx = 256, cy = 256, R = 250;

        const g = ctx.createRadialGradient(cx - 70, cy - 70, 10, cx, cy, R);
        g.addColorStop(0.00, '#fff5cc');
        g.addColorStop(0.35, '#ffd978');
        g.addColorStop(0.75, '#d8a848');
        g.addColorStop(1.00, '#9a6c20');
        ctx.fillStyle = g;
        ctx.beginPath();
        ctx.arc(cx, cy, R, 0, Math.PI * 2);
        ctx.fill();

        ctx.strokeStyle = 'rgba(255,240,180,.7)';
        ctx.lineWidth = 5;
        ctx.beginPath();
        ctx.arc(cx, cy, R * 0.92, 0, Math.PI * 2);
        ctx.stroke();

        ctx.strokeStyle = 'rgba(120,70,20,.55)';
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.arc(cx, cy, R * 0.89, 0, Math.PI * 2);
        ctx.stroke();

        ctx.font = 'bold 260px "Fraunces", Georgia, serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';

        ctx.fillStyle = 'rgba(80,45,10,.55)';
        ctx.fillText('₹', cx + 3, cy + 12);

        ctx.fillStyle = '#7a4a10';
        ctx.fillText('₹', cx, cy + 6);

        ctx.fillStyle = 'rgba(255,240,190,.85)';
        ctx.fillText('₹', cx - 2, cy + 2);

        ctx.fillStyle = '#a8702a';
        ctx.fillText('₹', cx - 1, cy + 6);

        ctx.fillStyle = 'rgba(255,250,220,.7)';
        ctx.fillText('₹', cx - 2, cy + 3);

        const hl = ctx.createRadialGradient(cx - 90, cy - 110, 10, cx - 90, cy - 110, 260);
        hl.addColorStop(0, 'rgba(255,255,255,.65)');
        hl.addColorStop(0.4, 'rgba(255,255,255,.15)');
        hl.addColorStop(1, 'rgba(255,255,255,0)');
        ctx.fillStyle = hl;
        ctx.beginPath();
        ctx.arc(cx, cy, R, 0, Math.PI * 2);
        ctx.fill();

        const sh = ctx.createRadialGradient(cx + 100, cy + 120, 20, cx + 100, cy + 120, 260);
        sh.addColorStop(0, 'rgba(60,30,5,.45)');
        sh.addColorStop(1, 'rgba(60,30,5,0)');
        ctx.fillStyle = sh;
        ctx.beginPath();
        ctx.arc(cx, cy, R, 0, Math.PI * 2);
        ctx.fill();

        return c;
    }

    const coinFaceTexture = new THREE.CanvasTexture(makeCoinFaceTexture());
    coinFaceTexture.colorSpace = THREE.SRGBColorSpace;
    coinFaceTexture.anisotropy = 16;

    const coinFaceMat = new THREE.MeshPhongMaterial({
        map: coinFaceTexture,
        shininess: 130,
        specular: 0xffe8a0,
        emissive: 0x2a1800,
        emissiveIntensity: 0.15
    });

    const coinEdgeMat = new THREE.MeshPhongMaterial({
        color: 0xb88a2a,
        shininess: 100,
        specular: 0xffd070,
        emissive: 0x201000,
        emissiveIntensity: 0.15
    });

    const coinRimMat = new THREE.MeshPhongMaterial({
        color: 0xffd878,
        shininess: 150,
        specular: 0xffffff,
        emissive: 0x3a2000,
        emissiveIntensity: 0.15
    });

    function makeCoin(radius) {
        const g = new THREE.Group();
        const thickness = radius * 0.30;

        const body = new THREE.Mesh(
            new THREE.CylinderGeometry(radius * 0.92, radius * 0.92, thickness, 64),
            coinEdgeMat
        );
        body.rotation.x = Math.PI / 2;
        g.add(body);

        const reedCount = 40;
        for (let i = 0; i < reedCount; i++) {
            const a = (i / reedCount) * Math.PI * 2;
            const rx = Math.cos(a) * radius * 0.92;
            const ry = Math.sin(a) * radius * 0.92;

            const reed = new THREE.Mesh(
                new THREE.BoxGeometry(radius * 0.028, thickness * 0.7, radius * 0.02),
                coinEdgeMat
            );
            reed.position.set(rx, ry, 0);
            reed.rotation.z = a;
            g.add(reed);
        }

        const rim = new THREE.Mesh(
            new THREE.TorusGeometry(radius * 0.92, radius * 0.018, 8, 64),
            coinRimMat
        );
        g.add(rim);

        const face = new THREE.Mesh(
            new THREE.CircleGeometry(radius * 0.90, 64),
            coinFaceMat
        );
        face.position.z = thickness / 2 + 0.002;
        g.add(face);

        const backMat = new THREE.MeshPhongMaterial({
            map: coinFaceTexture,
            shininess: 130,
            specular: 0xffe8a0,
            emissive: 0x2a1800,
            emissiveIntensity: 0.15
        });
        const face2 = new THREE.Mesh(
            new THREE.CircleGeometry(radius * 0.90, 64),
            backMat
        );
        face2.position.z = -thickness / 2 - 0.002;
        face2.rotation.y = Math.PI;
        g.add(face2);

        return g;
    }

    const coins = [];
    const coinCount = 9;
    for (let i = 0; i < coinCount; i++) {
        const c = makeCoin(0.18 + Math.random() * 0.05);
        c.userData = {
            angle: (i / coinCount) * Math.PI * 2,
            radius: 2.6 + Math.random() * 0.5,
            speed: 0.28 + Math.random() * 0.15,
            yBase: (Math.random() - 0.5) * 2.0,
            phase: Math.random() * Math.PI * 2,
            baseRotY: (Math.random() - 0.5) * 1.4,
            baseRotX: (Math.random() - 0.5) * 0.6,
            baseRotZ: (Math.random() - 0.5) * 0.3,
            oscY: 0.25,
            oscX: 0.15,
            oscZ: 0.15
        };
        scene.add(c);
        coins.push(c);
    }

    /* ---------- Particles ---------- */
    const particleColors = [0xffffff, 0xc4aee0, 0x10b981, 0xf43f5e, 0xd4ff3d];
    const particles = [];
    for (let i = 0; i < 30; i++) {
        const p = new THREE.Mesh(
            new THREE.SphereGeometry(0.01 + Math.random() * 0.015, 8, 8),
            new THREE.MeshBasicMaterial({
                color: particleColors[Math.floor(Math.random() * particleColors.length)],
                toneMapped: false
            })
        );
        p.userData = {
            base: new THREE.Vector3(
                (Math.random() - 0.5) * 8,
                (Math.random() - 0.5) * 7,
                (Math.random() - 0.5) * 4
            ),
            phase: Math.random() * Math.PI * 2,
            speed: 0.3 + Math.random() * 0.6
        };
        scene.add(p);
        particles.push(p);
    }

    /* ============================================================
       HTML CARD PARALLAX + ROTATION PRESERVATION
       ============================================================ */
    const cardDepths = [
        { el: document.querySelector('.card-total'),   d: 0.4, rot: '-3deg' },
        { el: document.querySelector('.card-expense'), d: 0.7, rot: '-2deg' },
        { el: document.querySelector('.card-chart'),   d: 0.6, rot: '2deg'  },
        { el: document.querySelector('.debit-card'),   d: 0.9, rot: '5deg'  }
    ].filter(x => x.el);

    /* ---------- Mouse parallax ---------- */
    let tx = 0, ty = 0, cx = 0, cy = 0;
    const hero = document.getElementById('ccHero');
    if (hero) {
        hero.addEventListener('mousemove', e => {
            const r = hero.getBoundingClientRect();
            tx = ((e.clientX - r.left) / r.width  - 0.5) * 2;
            ty = ((e.clientY - r.top)  / r.height - 0.5) * 2;
        });
        hero.addEventListener('mouseleave', () => { tx = 0; ty = 0; });
    }

    /* ---------- Resize ---------- */
    function resize() {
        const rect = canvas.getBoundingClientRect();
        const w = Math.max(rect.width, 1);
        const h = Math.max(rect.height, 1);
        camera.aspect = w / h;
        camera.updateProjectionMatrix();
        renderer.setSize(w, h, false);
    }
    resize();
    window.addEventListener('resize', resize);

    /* ---------- Animate ---------- */
    const clock = new THREE.Clock();
    let rafId = null, running = true;

    function animate() {
        const t = clock.getElapsedTime();

        cx += (tx - cx) * 0.06;
        cy += (ty - cy) * 0.06;

        camera.position.x = cx * 0.6;
        camera.position.y = -cy * 0.45;
        camera.lookAt(0, 0, 0);

        orbGroup.position.y = Math.sin(t * 0.55) * 0.06;

        planet.rotation.y = t * 0.08;
        cityPoints.rotation.y = t * 0.08;

        coins.forEach(c => {
            const d = c.userData;
            d.angle += d.speed * 0.015;

            const x = Math.cos(d.angle) * d.radius;
            const z = Math.sin(d.angle) * d.radius * 0.5;
            const y = d.yBase + Math.sin(t * 0.8 + d.phase) * 0.35;

            c.position.set(x, y, z);

            c.rotation.y = d.baseRotY + Math.sin(t * 0.5 + d.phase) * d.oscY;
            c.rotation.x = d.baseRotX + Math.sin(t * 0.6 + d.phase) * d.oscX;
            c.rotation.z = d.baseRotZ + Math.sin(t * 0.7 + d.phase) * d.oscZ;
        });

        particles.forEach(p => {
            const u = p.userData;
            p.position.set(
                u.base.x + Math.sin(t * u.speed + u.phase) * 0.25,
                u.base.y + Math.cos(t * u.speed * 0.7 + u.phase) * 0.3,
                u.base.z + Math.sin(t * u.speed * 0.5 + u.phase) * 0.2
            );
            const s = 0.7 + Math.sin(t * u.speed * 2 + u.phase) * 0.5;
            p.scale.setScalar(s);
        });

        cardDepths.forEach(x => {
            const px = (cx * x.d * 12).toFixed(2);
            const py = (cy * x.d * 10).toFixed(2);
            x.el.style.transform =
                `translate3d(${px}px, ${py}px, 0) rotate(${x.rot})`;
        });

        renderer.render(scene, camera);
        if (running) rafId = requestAnimationFrame(animate);
    }

    if ('IntersectionObserver' in window) {
        const io = new IntersectionObserver(entries => {
            const v = entries[0].isIntersecting;
            if (v && !running) { running = true; clock.start(); animate(); }
            else if (!v && running) { running = false; cancelAnimationFrame(rafId); }
        }, { threshold: 0 });
        io.observe(canvas);
    }

    animate();
})();