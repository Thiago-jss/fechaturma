# FechaTurma Desktop

Cliente WinForms (.NET Framework 4.8, C# 7.3) para a coordenação. A API deve estar disponível em `http://localhost:5184`. O executável não aceita outro host e não segue redirecionamentos nem usa proxy.

1. Abra `FechaTurma.Desktop.exe`. Digite a chave de acesso ou forneça `FT_DESKTOP_KEY` no ambiente do processo. O campo é mascarado e o aplicativo não grava a chave.
2. Clique em **Atualizar lista** e selecione uma turma pendente.
3. Confira os resultados, as contagens e o histórico. Clique em **Fechar turma** e confirme. O cliente envia a versão dos detalhes conferidos e só mostra sucesso quando a API retorna a turma fechada.
4. Em caso de conflito ou falha de comunicação, atualize a lista para verificar o estado antes de tentar novamente. O cliente não repete automaticamente a operação.

## Build e testes HTTP

A partir da raiz, com o SDK .NET 8:

```text
dotnet build src/FechaTurma.Desktop -c Release
dotnet run --project tests/FechaTurma.Desktop.Tests -c Release
```

O build produz `src/FechaTurma.Desktop/bin/Release/net48/FechaTurma.Desktop.exe`. Os testes usam um servidor HTTP real em loopback com porta dinâmica e compilam o mesmo código REST do cliente em `net8.0`; não executam WinForms nem substituem o smoke no Windows.

## Smoke no Windows

Pré-requisitos: Windows com .NET Framework 4.8 e acesso a um desktop Win32, API no IIS em `http://localhost:5184`, chave de coordenação em `FT_DESKTOP_KEY` e ao menos uma turma pendente acessível. Não é necessária interação de teclado/mouse. `--smoke` aciona os controles de atualização e seleção, verifica a renderização de resultados/histórico e testa o cancelamento da confirmação, sem emitir POST.

```powershell
$exe = Join-Path $PWD 'src/FechaTurma.Desktop/bin/Release/net48/FechaTurma.Desktop.exe'
$p = Start-Process -FilePath $exe -ArgumentList '--smoke' -PassThru
if (-not $p.WaitForExit(35000)) { $p.Kill(); throw 'Tempo limite do smoke WinForms' }
if ($p.ExitCode -ne 0) { throw "Smoke WinForms falhou (código $($p.ExitCode))" }
```

**Somente com dados descartáveis:** substitua `-ArgumentList '--smoke'` por `-ArgumentList @('--smoke-close', $fixtureId)`, onde `$fixtureId` é o UUID de uma turma pendente criada para o teste. Esse modo opera os mesmos controles, confirma programaticamente somente a turma informada, fecha-a de fato e consulta a API novamente para verificar o fechamento e o histórico. Códigos: `0` passou, `1` falhou, `2` argumento ou chave de ambiente ausente/inválido, `124` tempo limite interno.
