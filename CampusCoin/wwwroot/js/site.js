(() => {
  const toast = (message="Changes saved") => {
    const el=document.querySelector("[data-toast]");
    if(!el) return;
    el.querySelector("span").textContent=message;
    el.classList.add("show");
    clearTimeout(window.__toast);
    window.__toast=setTimeout(()=>el.classList.remove("show"),2400);
  };
  document.querySelectorAll("[data-toast-trigger]").forEach(b=>b.addEventListener("click",()=>toast("Done — your action was captured.")));
  document.querySelectorAll("[data-demo-form]").forEach(f=>f.addEventListener("submit",e=>{e.preventDefault();toast("Demo action complete — connect your backend to persist this.");}));
  document.querySelectorAll("[data-password]").forEach(b=>b.addEventListener("click",()=>{
    const i=b.parentElement.querySelector("input"); i.type=i.type==="password"?"text":"password";
    b.innerHTML=i.type==="password"?'<i class="ri-eye-line"></i>':'<i class="ri-eye-off-line"></i>';
  }));
  document.querySelector("[data-theme-toggle]")?.addEventListener("click",()=>{
    document.body.classList.toggle("dark");
    localStorage.setItem("cc-dark",document.body.classList.contains("dark"));
  });
  document.querySelectorAll("[data-theme-toggle]").forEach(b=>b.addEventListener("change",()=>{
    document.body.classList.toggle("dark",b.checked); localStorage.setItem("cc-dark",b.checked);
  }));
  if(localStorage.getItem("cc-dark")==="true") document.body.classList.add("dark");
  document.querySelector("[data-font-toggle]")?.addEventListener("change",e=>document.body.classList.toggle("large-text",e.target.checked));
  document.querySelector("[data-sidebar-toggle]")?.addEventListener("click",()=>document.querySelector(".sidebar")?.classList.toggle("open"));
  const search=document.querySelector("[data-table-search]");
  search?.addEventListener("input",()=>{
    const q=search.value.toLowerCase();
    document.querySelectorAll(".searchable").forEach(r=>r.style.display=r.innerText.toLowerCase().includes(q)?"grid":"none");
  });
  document.querySelector("[data-file-button]")?.addEventListener("click",()=>document.querySelector("#csvFile")?.click());
  document.querySelector("#csvFile")?.addEventListener("change",e=>{ if(e.target.files?.[0]) toast(e.target.files[0].name+" selected"); });
  const drop=document.querySelector("[data-drop]");
  if(drop){
    ["dragenter","dragover"].forEach(x=>drop.addEventListener(x,e=>{e.preventDefault();drop.style.transform="scale(1.01)"}));
    ["dragleave","drop"].forEach(x=>drop.addEventListener(x,e=>{e.preventDefault();drop.style.transform=""}));
    drop.addEventListener("drop",e=>{const f=e.dataTransfer.files?.[0];if(f)toast(f.name+" ready to import");});
  }
  document.querySelectorAll(".nav-link").forEach(a=>{
    if(a.href===location.href || (location.pathname.startsWith(a.getAttribute("href")) && a.getAttribute("href")!=="/")) a.classList.add("active");
  });
})();
