// wwwroot/js/campus-coin.js
(function () {
    /* ═══════════ Hero reveal ═══════════ */
    const heroCopy = document.getElementById('ccHeroCopy');
    const scene    = document.getElementById('ccScene');
    const reveal = () => {
        heroCopy?.classList.add('in');
        scene?.classList.add('in');
    };
    window.addEventListener('load', () => setTimeout(reveal, 120));
    if (document.readyState === 'complete') setTimeout(reveal, 120);

    /* ═══════════ Number count-up ═══════════ */
    function animateCount(el, target, duration, prefix, suffix) {
        const start = performance.now();
        const step = (now) => {
            const t = Math.min(1, (now - start) / duration);
            const eased = 1 - Math.pow(1 - t, 3);
            const value = Math.round(target * eased);
            el.textContent = (prefix || '') + value.toLocaleString('en-IN') + (suffix || '');
            if (t < 1) requestAnimationFrame(step);
        };
        requestAnimationFrame(step);
    }

    function runCountUps() {
        document.querySelectorAll('[data-count]').forEach((el, i) => {
            const target = parseFloat(el.dataset.count);
            const prefix = el.dataset.prefix || '';
            const suffix = el.dataset.suffix || '';
            const delay  = 800 + i * 120;
            setTimeout(() => {
                el.textContent = prefix + '0' + suffix;
                animateCount(el, target, 1400, prefix, suffix);
            }, delay);
        });
    }

    /* ═══════════ How-it-works timeline ═══════════ */
    const howTimeline = document.getElementById('howTimeline');
    if (howTimeline) {
        const steps = howTimeline.querySelectorAll('.how-step');
        const howObserver = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    howTimeline.classList.add('in-view');
                    steps.forEach((step, i) => {
                        setTimeout(() => step.classList.add('in-view'), i * 250);
                    });
                    howObserver.unobserve(entry.target);
                }
            });
        }, { threshold: 0.2 });
        howObserver.observe(howTimeline);
    }

    /* ═══════════ Monthly chart bars in hero ═══════════ */
    function runBars() {
        document.querySelectorAll('.card-chart .bar-chart span').forEach((bar, i) => {
            setTimeout(() => {
                const h = getComputedStyle(bar).getPropertyValue('--h').trim();
                bar.style.height = h;
            }, 1000 + i * 80);
        });
    }

    window.addEventListener('load', () => {
        setTimeout(runCountUps, 400);
        setTimeout(runBars, 400);
    });
    if (document.readyState === 'complete') {
        setTimeout(runCountUps, 400);
        setTimeout(runBars, 400);
    }

    /* ═══════════ Contact form ═══════════ */
    (function () {
        const topicBtns = document.querySelectorAll('.cf-topic');
        const subjectInput = document.getElementById('cf-subject');
        const knownTopics = ['Question', 'Feedback', 'Bug report', 'Just saying hi'];

        topicBtns.forEach(btn => {
            btn.addEventListener('click', () => {
                topicBtns.forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                const topic = btn.dataset.topic || '';
                if (subjectInput && topic) {
                    const existing = subjectInput.value.trim();
                    if (!existing || knownTopics.includes(existing)) {
                        subjectInput.value = topic;
                    }
                }
            });
        });

        const msg = document.getElementById('cf-message');
        const countNum = document.getElementById('cf-count-num');
        if (msg && countNum) {
            const update = () => { countNum.textContent = msg.value.length; };
            msg.addEventListener('input', update);
            update();
        }
    })();

    /* ═══════════ Enhanced scroll effects ═══════════ */
    function initEnhancedScrollEffects() {
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
        if (!('IntersectionObserver' in window)) return;

        /* Progress bar */
        const bar = document.createElement('div');
        bar.className = 'cc-scroll-progress';
        document.body.appendChild(bar);

        let rafId = null;
        function updateBar() {
            const h = document.documentElement.scrollHeight - window.innerHeight;
            const pct = h > 0 ? (window.scrollY / h) * 100 : 0;
            bar.style.width = pct + '%';
            rafId = null;
        }
        window.addEventListener('scroll', () => {
            if (!rafId) rafId = requestAnimationFrame(updateBar);
        }, { passive: true });
        updateBar();

        /* Section headers */
        document.querySelectorAll('.section-head').forEach(h => h.classList.add('reveal-group'));
        const headIO = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('in-view');
                    headIO.unobserve(entry.target);
                }
            });
        }, { threshold: 0.15, rootMargin: '0px 0px -60px 0px' });
        document.querySelectorAll('.section-head.reveal-group').forEach(h => headIO.observe(h));

        /* Generic reveal */
        const config = [
            { sel: '.bento-card',          dir: 'from-zoom reveal-blur' },
            { sel: '.how-step',            dir: ''                      },
            { sel: '.how-summary',         dir: 'from-zoom'             },
            { sel: '.about-story-block',   dir: 'from-left reveal-blur' },
            { sel: '.about-stat',          dir: 'from-left'             },
            { sel: '.about-value',         dir: 'from-right'            },
            { sel: '.sm-root-node',        dir: 'from-zoom reveal-blur' },
            { sel: '.sitemap-col',         dir: 'reveal-blur'           },
            { sel: '.sitemap-note',        dir: 'from-zoom'             },
            { sel: '.contact-method',      dir: 'from-left'             },
            { sel: '.contact-form-wrap',   dir: 'from-right reveal-blur'},
            { sel: '.contact-response',    dir: 'from-left'             },
            { sel: '.cta',                 dir: 'from-zoom reveal-blur' }
        ];

        const elements = [];
        config.forEach(c => {
            document.querySelectorAll(c.sel).forEach(el => {
                if (el.classList.contains('reveal-item')) return;
                el.classList.add('reveal-item', 'reveal-hidden');
                c.dir.split(' ').filter(Boolean).forEach(d => el.classList.add(d));
                elements.push(el);
            });
        });

        /* Mouse glow */
        document.querySelectorAll('.bento-card').forEach(card => {
            card.addEventListener('mousemove', e => {
                const r = card.getBoundingClientRect();
                card.style.setProperty('--mx', ((e.clientX - r.left) / r.width * 100) + '%');
                card.style.setProperty('--my', ((e.clientY - r.top) / r.height * 100) + '%');
            });
        });

        /* Reveal + inner animations */
        function triggerInnerAnimations(card) {
            card.querySelectorAll('[data-count-anim]').forEach(el => {
                if (el.dataset.animated === 'true') return;
                el.dataset.animated = 'true';
                const target = parseFloat(el.dataset.countAnim);
                const prefix = el.dataset.prefix || '';
                const start = performance.now();
                const duration = 1600;
                function step(now) {
                    const t = Math.min(1, (now - start) / duration);
                    const eased = 1 - Math.pow(1 - t, 3);
                    const v = Math.round(target * eased);
                    el.textContent = prefix + v.toLocaleString('en-IN');
                    if (t < 1) requestAnimationFrame(step);
                }
                requestAnimationFrame(step);
            });

            card.querySelectorAll('.budget-ring-fill[data-ring-pct]').forEach(ring => {
                const pct = parseFloat(ring.dataset.ringPct);
                const circumference = 264;
                const offset = circumference - (circumference * pct / 100);
                const gradId = ring.dataset.ringGrad;
                if (gradId) ring.setAttribute('stroke', `url(#${gradId})`);
                setTimeout(() => { ring.style.strokeDashoffset = offset; }, 100);
            });
        }

        if (elements.length) {
            const io = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    const el = entry.target;
                    let delay = 0;
                    if (el.parentElement) {
                        const sibs = Array.from(el.parentElement.children)
                            .filter(c => c.classList.contains('reveal-item'));
                        const idx = sibs.indexOf(el);
                        if (idx > 0) delay = Math.min(idx, 8) * 120;
                    }
                    setTimeout(() => {
                        el.classList.remove('reveal-hidden');
                        setTimeout(() => {
                            el.classList.add('in-view');
                            triggerInnerAnimations(el);
                        }, 400);
                    }, delay);
                    io.unobserve(el);
                });
            }, { threshold: 0.12, rootMargin: '0px 0px -80px 0px' });

            elements.forEach(el => io.observe(el));

            setTimeout(() => {
                elements.forEach(el => {
                    const r = el.getBoundingClientRect();
                    if (r.top < window.innerHeight && r.bottom > 0) {
                        el.classList.remove('reveal-hidden');
                        el.classList.add('in-view');
                        triggerInnerAnimations(el);
                        io.unobserve(el);
                    }
                });
            }, 500);
        }

        /* AI section */
        const aiChat = document.querySelector('.ai-chat');
        const aiFeatures = document.querySelectorAll('.ai-feature');
        const aiTrust = document.querySelector('.ai-trust');

        if (aiChat) {
            const aiIO = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    entry.target.classList.add('in-view');
                    aiIO.unobserve(entry.target);
                });
            }, { threshold: 0.15, rootMargin: '0px 0px -60px 0px' });
            aiIO.observe(aiChat);
        }

        if (aiFeatures.length) {
            const featIO = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    entry.target.classList.add('in-view');
                    featIO.unobserve(entry.target);
                });
            }, { threshold: 0.2, rootMargin: '0px 0px -40px 0px' });
            aiFeatures.forEach(f => featIO.observe(f));
        }

        if (aiTrust) {
            const trustIO = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    entry.target.classList.add('in-view');
                    trustIO.unobserve(entry.target);
                });
            }, { threshold: 0.2 });
            trustIO.observe(aiTrust);
        }

        /* Categories */
        document.querySelectorAll('.cat-panel').forEach(p => {
            const io = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    entry.target.classList.add('in-view');
                    io.unobserve(entry.target);
                });
            }, { threshold: 0.15, rootMargin: '0px 0px -60px 0px' });
            io.observe(p);
        });

        const catCustom = document.querySelector('.cat-custom');
        if (catCustom) {
            const custIO = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    entry.target.classList.add('in-view');
                    custIO.unobserve(entry.target);
                });
            }, { threshold: 0.2 });
            custIO.observe(catCustom);
        }

        /* Testimonials */
        const testSummary = document.querySelector('.test-summary');
        const testCards = document.querySelectorAll('.test-equal .test');
        const testTrust = document.querySelector('.test-trust');

        if (testSummary) {
            const sumIO = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    entry.target.classList.add('in-view');
                    sumIO.unobserve(entry.target);
                });
            }, { threshold: 0.15, rootMargin: '0px 0px -60px 0px' });
            sumIO.observe(testSummary);
        }

        if (testCards.length) {
            const cardIO = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    entry.target.classList.add('in-view');
                    cardIO.unobserve(entry.target);
                });
            }, { threshold: 0.15, rootMargin: '0px 0px -60px 0px' });
            testCards.forEach(t => cardIO.observe(t));
        }

        if (testTrust) {
            const trustIO = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    entry.target.classList.add('in-view');
                    trustIO.unobserve(entry.target);
                });
            }, { threshold: 0.2 });
            trustIO.observe(testTrust);
        }

        /* Sitemap columns */
        document.querySelectorAll('.sitemap-col').forEach(col => {
            const io = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    entry.target.classList.add('in-view');
                    io.unobserve(entry.target);
                });
            }, { threshold: 0.15, rootMargin: '0px 0px -60px 0px' });
            io.observe(col);
        });

        /* Category totals count-up */
        document.querySelectorAll('[data-cat-count]').forEach(el => {
            const target = parseFloat(el.dataset.catCount);
            const prefix = el.dataset.prefix || '';
            const catPanel = el.closest('.cat-panel');
            if (!catPanel) return;

            const counterIO = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;
                    if (el.dataset.animated === 'true') return;
                    el.dataset.animated = 'true';
                    const start = performance.now();
                    const duration = 1600;
                    function step(now) {
                        const t = Math.min(1, (now - start) / duration);
                        const eased = 1 - Math.pow(1 - t, 3);
                        const v = Math.round(target * eased);
                        el.textContent = prefix + v.toLocaleString('en-IN');
                        if (t < 1) requestAnimationFrame(step);
                    }
                    requestAnimationFrame(step);
                    counterIO.unobserve(entry.target);
                });
            }, { threshold: 0.2 });
            counterIO.observe(el);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initEnhancedScrollEffects);
    } else {
        initEnhancedScrollEffects();
    }
})();