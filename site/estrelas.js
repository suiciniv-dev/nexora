(() => {
  const alvo = document.getElementById("estrelas");
  if (!alvo) return;
  const idioma = document.documentElement.lang === "en" ? "en" : "pt-BR";
  const mostrar = (n) => {
    if (typeof n !== "number" || n < 0) return;
    alvo.querySelector("b").textContent = new Intl.NumberFormat(idioma, { notation: n >= 1000 ? "compact" : "standard", maximumFractionDigits: 1 }).format(n);
    alvo.hidden = false;
  };
  const chave = "nexora-estrelas";
  try {
    const salvo = JSON.parse(localStorage.getItem(chave) || "null");
    if (salvo && Date.now() - salvo.em < 3600000) { mostrar(salvo.n); return; }
  } catch (e) {}
  fetch("https://api.github.com/repos/suiciniv-dev/nexora", { headers: { Accept: "application/vnd.github+json" } })
    .then((r) => (r.ok ? r.json() : null))
    .then((dados) => {
      if (!dados || typeof dados.stargazers_count !== "number") return;
      mostrar(dados.stargazers_count);
      try { localStorage.setItem(chave, JSON.stringify({ n: dados.stargazers_count, em: Date.now() })); } catch (e) {}
    })
    .catch(() => {});
})();
