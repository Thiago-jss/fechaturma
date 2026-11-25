# language: pt
Funcionalidade: Fechar uma turma com histórico rastreável
  O resultado usa o retrato conferido, sem alterar a origem acadêmica.

  Cenário: A secretaria não pode importar um retrato incompleto
    Dado que a turma PI-02 tem uma matrícula sem nota
    Quando a secretaria gera a prévia
    Então a matrícula aparece com a pendência "Nota ausente"
    E nenhuma importação parcial é gravada

  Cenário: Reenviar a mesma prévia não duplica a turma
    Dado que foi importado um retrato válido de PI-01
    Quando a secretaria importa novamente o mesmo conteúdo
    Então o fechamento mantém o mesmo identificador
    E existe apenas um evento de importação

  Cenário: Apenas uma decisão concorrente fecha o retrato
    Dado que a versão 1 de PI-01 está pendente
    Quando chegam duas solicitações da coordenação com a versão 1
    Então exatamente uma solicitação fecha a turma na versão 2
    E a outra recebe um conflito
    E a ata registra uma única decisão de fechamento

  Cenário: Uma chave não confere autoridade de outra função
    Dado que a secretaria importou a turma PI-01
    Quando alguém usa a chave da secretaria para solicitar o fechamento
    Então a API retorna acesso negado
    E a turma continua pendente
