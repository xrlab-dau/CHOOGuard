class PresentationController {
  constructor() {
    this.slides = [...document.querySelectorAll('.slide')];
    this.index = 0;
    this.output = document.querySelector('#index');
    this.notes = document.querySelector('#speaker-notes');
    this.noteButton = document.querySelector('#notes');
    this.previousFocus = null;
    this.lastWheel = 0;
    this.resize = () => {
      const scale = Math.min(innerWidth / 1920, innerHeight / 1080);
      document.querySelectorAll('.slide-content').forEach(e => { e.style.transform = `scale(${scale})`; });
    };
    addEventListener('resize', this.resize);
    this.resize();
    this.observer = new IntersectionObserver(entries => entries.forEach(e => {
      if (e.isIntersecting) {
        e.target.classList.add('revealed');
        this.index = this.slides.indexOf(e.target);
        this.sync();
      }
    }), { threshold: .6 });
    this.slides.forEach(s => this.observer.observe(s));
    addEventListener('keydown', e => {
      if (e.altKey && !e.ctrlKey && !e.metaKey && e.key.toLowerCase() === 'n') {
        e.preventDefault(); this.toggleNotes(); return;
      }
      if (e.altKey && !e.ctrlKey && !e.metaKey && e.key.toLowerCase() === 'f') {
        e.preventDefault(); document.documentElement.requestFullscreen?.(); return;
      }
      if (this.notes.classList.contains('open')) {
        if (e.key === 'Escape') { e.preventDefault(); this.toggleNotes(false); }
        return; // Native arrows/PageDown scroll the focused notes.
      }
      if (e.altKey || e.ctrlKey || e.metaKey) return;
      const target = e.target instanceof Element ? e.target : null;
      if (target?.closest('button,a,input,select,textarea,[contenteditable="true"],[role="textbox"]')) return;
      if (['ArrowRight', 'ArrowDown', 'PageDown', ' '].includes(e.key)) { e.preventDefault(); this.go(1); }
      else if (['ArrowLeft', 'ArrowUp', 'PageUp'].includes(e.key)) { e.preventDefault(); this.go(-1); }
      else if (e.key === 'Home') { e.preventDefault(); this.jump(0); }
      else if (e.key === 'End') { e.preventDefault(); this.jump(this.slides.length - 1); }
    });
    addEventListener('wheel', e => {
      if (this.notes.classList.contains('open')) return;
      if (Date.now() - this.lastWheel > 650 && Math.abs(e.deltaY) > 20) {
        e.preventDefault(); this.go(e.deltaY > 0 ? 1 : -1); this.lastWheel = Date.now();
      }
    }, { passive: false });
    let touchY = 0;
    addEventListener('touchstart', e => { touchY = e.changedTouches[0].clientY; }, { passive: true });
    addEventListener('touchend', e => {
      if (this.notes.classList.contains('open')) return;
      const dy = touchY - e.changedTouches[0].clientY;
      if (Math.abs(dy) > 45) this.go(dy > 0 ? 1 : -1);
    }, { passive: true });
    document.querySelector('#prev').onclick = () => this.go(-1);
    document.querySelector('#next').onclick = () => this.go(1);
    this.noteButton.onclick = () => this.toggleNotes();
    document.querySelector('#fullscreen').onclick = () => document.documentElement.requestFullscreen?.();
    document.querySelector('#print').onclick = () => print();
    this.sync();
  }
  sync() {
    this.output.textContent = `${String(this.index + 1).padStart(2, '0')} / ${this.slides.length}`;
    this.notes.textContent = window.SLIDE_NOTES[this.index];
    this.noteButton.setAttribute('aria-expanded', String(this.notes.classList.contains('open')));
    document.title = `CHOOGuard · ${this.index + 1} · ${this.slides[this.index].getAttribute('aria-label')}`;
  }
  go(delta) { this.jump(this.index + delta); }
  jump(index) {
    this.index = Math.max(0, Math.min(index, this.slides.length - 1));
    this.slides[this.index].scrollIntoView({ behavior: matchMedia('(prefers-reduced-motion:reduce)').matches ? 'instant' : 'smooth' });
    this.sync();
  }
  toggleNotes(force) {
    const open = force ?? !this.notes.classList.contains('open');
    if (open) this.previousFocus = document.activeElement;
    this.notes.classList.toggle('open', open);
    this.sync();
    if (open) this.notes.focus();
    else (this.previousFocus instanceof HTMLElement ? this.previousFocus : this.noteButton).focus();
  }
}
window.deck = new PresentationController();
