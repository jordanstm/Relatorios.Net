# Firebird Data Reporter 📊

Um gerador de relatórios dinâmico e visual para bancos de dados **Firebird**, desenvolvido em **C# / WPF** com **.NET 10**.

## ✨ Funcionalidades

- **Construtor Visual**: Arraste tabelas para o canvas e visualize as relações.
- **Mapeamento de Relações**: Suporte a Foreign Keys automáticas e links manuais.
- **Filtros e Agrupamentos**: Defina condições de filtro e agrupamento de forma simples.
- **Exportação Multiformato**:
  - **PDF**: Gerado via QuestPDF com suporte a Layout responsivo.
  - **HTML**: Pré-visualização rica com tema moderno.
  - **Excel/CSV**: Exportação rápida de dados.
- **Persistência**: Salve e carregue seus projetos de relatório (arquivos .json).

## 🛠️ Tecnologias Utilizadas

- **.NET 10**
- **WPF (Windows Presentation Foundation)**
- **QuestPDF**: Motor de geração de documentos.
- **Firebird Client**: Conectividade robusta com Firebird SQL.

## 🚀 Como Executar

1. Certifique-se de ter o **SDK do .NET 10** instalado.
2. Clone o repositório:
   ```bash
   git clone https://github.com/jordanstm/Relatorios.Net.git
   ```
3. Abra o projeto no Visual Studio 2022 ou VS Code.
4. Restaure os pacotes e execute:
   ```bash
   dotnet run
   ```

## 📄 Licença

Este projeto foi desenvolvido como uma ferramenta de automação de relatórios. Sinta-se à vontade para usar e adaptar!
