# Fonte acadêmica opcional — TOTVS Linha RM

O laboratório usa Oracle como uma origem acadêmica fictícia e SQL Server como destino. A demo rápida no Windows também pode usar a fixture em memória. Esta alternativa é um **contrato de importação somente leitura**, não um cliente SOAP habilitado. Não exige licença, credenciais, instância RM nem dados reais para avaliar a amostra.

## Contrato público e escolha

- **Preferência: `wsConsultaSQL.RealizarConsultaSQL`.** A Central de Atendimento TOTVS documenta uma chamada SOAP 1.1, com a operação e os parâmetros no namespace `http://www.totvs.com/`: `codSentenca` (`String`, código da sentença cadastrada), `codColigada` (`Int`, coligada à qual a sentença pertence), `codSistema` (`String`, sistema ao qual pertence) e `parameters` (`String`, com `;` entre parâmetros). A amostra oficial retorna `RealizarConsultaSQLResponse/RealizarConsultaSQLResult` com XML textual em CDATA, estruturado como `NewDataSet/Resultado`. Os campos da amostra pertencem à consulta exemplificada, não a um esquema acadêmico publicado.[1]
- **Alternativa: `wsDataServer.ReadView`.** O TDN descreve retorno em XML conforme contexto e filtro e lista `DataServerName`, `Filtro` e `Contexto` como parâmetros `String`. O mesmo serviço oferece operações distintas de escrita; elas não fazem parte desta integração. O TDN também alerta para diferenças de cultura nos decimais e recomenda `Invariant` para novas integrações DataServer.[2][3]
- A listagem pública de `wsEdu` consultada documenta boletos e simulação de contrato, mas não lista uma operação de extração de matrícula, nota e frequência para o fechamento.[3]

O material consultado não estabelece a `SOAPAction`, o binding e a autenticação efetivamente configurados para um ambiente alvo, nem um código de consulta ou DataServer acadêmico aprovado para este projeto. Por isso não há transporte SOAP no demo. A `codColigada` do serviço identifica a coligada **da sentença**; não se deve assumir que equivale a `ClassKey.Organization` ou que, sozinha, restringe as linhas retornadas.[1]

## Handoff para `IAcademicSource`

A equipe responsável pelo RM deve aprovar uma sentença de leitura já cadastrada que restrinja a consulta por **organização, período e turma**, e devolva esses três valores em cada linha para conferência. Os nomes dos parâmetros, o código da sentença, sua coligada, seu sistema e os aliases da projeção devem vir de configuração aprovada; nenhum valor do exemplo oficial vira padrão. Não receber SQL, filtros livres ou identificadores do usuário, nem concatenar entradas na sentença. O operador deve validar se os códigos informados são representáveis em `parameters` sem ambiguidade de delimitadores.

O contrato abaixo é do FechaTurma, **não do RM**. O importador futuro normaliza o resultado da consulta para este formato antes de construir `AcademicRecord`. Cada linha fornece `registration`, `name`, `title`, `grade` e `attendance` ao construtor; `title` representa o título da turma. `registration` é texto e preserva zeros à esquerda. `grade` e `attendance` ausentes viram `null`; zero é um valor válido. A frequência é expressa em pontos percentuais (0 a 100); qualquer conversão da unidade de origem precisa ser acordada. A escala da nota deve ser acordada para converter ao formato local, de 0 a 10.

**Fixture sintética — XML normalizado, não envelope SOAP:**

```xml
<AcademicImport organization="1" term="2099.1" classCode="DEMO-A">
  <Row>
    <Registration>0001</Registration>
    <Name>Pessoa Fictícia 01</Name>
    <Title>Turma demonstrativa A</Title>
    <Grade>8.50</Grade>
    <Attendance>95.00</Attendance>
  </Row>
  <Row>
    <Registration>0002</Registration>
    <Name>Pessoa Fictícia 02</Name>
    <Title>Turma demonstrativa A</Title>
    <Grade>0</Grade>
  </Row>
</AcademicImport>
```

**XSD da projeção local:**

```xsd
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:simpleType name="RequiredText">
    <xs:restriction base="xs:string">
      <xs:minLength value="1"/>
      <xs:maxLength value="256"/>
    </xs:restriction>
  </xs:simpleType>
  <xs:simpleType name="AttendancePercent">
    <xs:restriction base="xs:decimal">
      <xs:minInclusive value="0"/>
      <xs:maxInclusive value="100"/>
      <xs:fractionDigits value="2"/>
    </xs:restriction>
  </xs:simpleType>
  <xs:simpleType name="Grade">
    <xs:restriction base="xs:decimal">
      <xs:minInclusive value="0"/>
      <xs:maxInclusive value="10"/>
      <xs:fractionDigits value="2"/>
    </xs:restriction>
  </xs:simpleType>
  <xs:element name="AcademicImport">
    <xs:complexType>
      <xs:sequence>
        <xs:element name="Row" minOccurs="0" maxOccurs="200">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="Registration" type="RequiredText"/>
              <xs:element name="Name" type="RequiredText"/>
              <xs:element name="Title" type="RequiredText"/>
              <xs:element name="Grade" type="Grade" minOccurs="0"/>
              <xs:element name="Attendance" type="AttendancePercent" minOccurs="0"/>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
      </xs:sequence>
      <xs:attribute name="organization" type="xs:int" use="required"/>
      <xs:attribute name="term" type="RequiredText" use="required"/>
      <xs:attribute name="classCode" type="RequiredText" use="required"/>
    </xs:complexType>
    <xs:unique name="UniqueRegistration">
      <xs:selector xpath="Row"/>
      <xs:field xpath="Registration"/>
    </xs:unique>
  </xs:element>
</xs:schema>
```

**Regras adicionais para o adaptador:**

1. Conferir escopo em **cada linha de origem**, antes de normalizar; não fabricar os campos de escopo copiando apenas a chave de entrada. A chave da projeção deve corresponder a `ClassKey.Organization`, `.Term` e `.ClassCode`. Não devolver lote parcial em caso de erro.
2. Rejeitar campos obrigatórios em branco e decimais não representáveis sem perda em `decimal` do .NET. Usar cultura invariável na projeção, sem separador de milhar. Duplicatas de matrícula são erro; não fazer deduplicação silenciosa. O limite de 200 linhas é local: excesso é erro, não truncamento. Só retornar lista vazia quando houver resultado válido que confirme zero linhas; falha, payload vazio ou estrutura inesperada não significam turma vazia.
3. Antes de qualquer homologação, obter WSDL/binding e resposta sanitizada da versão e ambiente autorizados. Validar operação, ação SOAP, autenticação, permissões mínimas, coligada da sentença, nomes, aliases, nulos, escalas e comportamento sem resultados. Só então compor um `IAcademicSource` opcional, sem alterar a composição padrão.
4. Para o eventual transporte, configurar endpoint HTTPS autorizado, validação normal do certificado, autenticação conforme o binding, segredos fora do código, redirecionamento desabilitado, timeout total e limite de bytes durante a leitura (inclusive após descompressão, se habilitada). Tratar `Fault` separadamente de resultado. Desabilitar DTD e entidades externas tanto no envelope quanto no XML interno, limitar profundidade e caracteres e validar namespaces e formas esperadas. Nenhum log deve conter resposta bruta, dados acadêmicos ou segredos.
5. Antes de habilitar, testar com resposta sintética: turma válida e vazia, zero versus nulo, escopo divergente, duplicata, decimal inválido, `Fault`, resposta acima do limite, timeout, redirecionamento e XML com DTD. Não usar RM real para validar a demo.

## Sources

[1] https://centraldeatendimento.totvs.com/hc/pt-br/articles/360016515671-Cross-Segmentos-Backoffice-RM-Integra%C3%A7%C3%A3o-WebServices-wsConsultaSQL — Central de Atendimento TOTVS — WebServices: wsConsultaSQL
[2] https://tdn.totvs.com/display/LRM/TBC+-+Web+Service+DataServer — TDN — TBC: Web Service DataServer
[3] https://tdn.totvs.com/display/public/LRM/Web+Services+do+TBC — TDN — Web Services do TBC
