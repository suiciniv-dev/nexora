(() => {
  const pagina = document.documentElement.lang === "en" ? "en" : "pt";
  const salvar = (l) => { try { localStorage.setItem("lang", l); } catch (e) {} };
  let salvo = null;
  try { salvo = localStorage.getItem("lang"); } catch (e) {}
  const sugestao = document.getElementById("sugestao");
  document.querySelectorAll("a[data-idioma]").forEach((a) => a.addEventListener("click", (e) => {
    salvar(a.dataset.idioma);
    if (a.dataset.idioma === pagina) { e.preventDefault(); if (sugestao) sugestao.hidden = true; return; }
    a.href = a.getAttribute("href").split("#")[0] + location.hash;
  }));
  if (!sugestao) return;
  sugestao.querySelector("button").addEventListener("click", () => { salvar(pagina); sugestao.hidden = true; });
  const lista = navigator.languages && navigator.languages.length ? navigator.languages : [navigator.language || ""];
  const navegador = lista.some((l) => /^pt\b/i.test(l)) ? "pt" : "en";
  if (!salvo && navegador !== pagina && !/[?&]rec\b/.test(location.search)) sugestao.hidden = false;
})();
