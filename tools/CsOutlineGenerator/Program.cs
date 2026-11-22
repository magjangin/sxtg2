using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsOutlineGenerator
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("==================================================");
            Console.WriteLine(" Roslyn C# AI Outline Generator (Token Saver)");
            Console.WriteLine("==================================================");

            string currentDir = Directory.GetCurrentDirectory();
            string repoRoot = currentDir;
            if (!Directory.Exists(Path.Combine(repoRoot, "sxtg2")) && Directory.Exists(Path.Combine(repoRoot, "..", "sxtg2")))
            {
                repoRoot = Path.GetFullPath(Path.Combine(repoRoot, ".."));
            }

            string defaultInputDir = Path.Combine(repoRoot, "sxtg2");
            string defaultOutputDir = Path.Combine(repoRoot, "sxtg2-outline");

            string inputDir = args.Length > 0 ? Path.GetFullPath(args[0]) : defaultInputDir;
            string outputDir = args.Length > 1 ? Path.GetFullPath(args[1]) : defaultOutputDir;

            if (!Directory.Exists(inputDir))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] 입력 디렉터리를 찾을 수 없습니다: {inputDir}");
                Console.ResetColor();
                return 1;
            }

            Console.WriteLine($"[INFO] 입력 경로: {inputDir}");
            Console.WriteLine($"[INFO] 출력 경로: {outputDir}");

            var csFiles = Directory.GetFiles(inputDir, "*.cs", SearchOption.AllDirectories);
            Console.WriteLine($"[INFO] 대상 C# 소스 파일: {csFiles.Length}개 발견");

            if (csFiles.Length == 0)
            {
                Console.WriteLine("[WARN] 변환할 .cs 파일이 없습니다.");
                return 0;
            }

            if (Directory.Exists(outputDir))
            {
                try
                {
                    Directory.Delete(outputDir, recursive: true);
                }
                catch
                {
                    // 기존 디렉터리 삭제 실패 시 덮어쓰기
                }
            }
            Directory.CreateDirectory(outputDir);

            long originalTotalBytes = 0;
            long outlineTotalBytes = 0;
            int successCount = 0;
            var failureList = new ConcurrentBag<string>();

            var rewriter = new OutlineSyntaxRewriter();
            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);

            Console.WriteLine("[INFO] Roslyn 기반 AST 아웃라인 변환 시작...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Parallel.ForEach(csFiles, filePath =>
            {
                try
                {
                    string code = File.ReadAllText(filePath, Encoding.UTF8);
                    long fileOriginalBytes = Encoding.UTF8.GetByteCount(code);
                    System.Threading.Interlocked.Add(ref originalTotalBytes, fileOriginalBytes);

                    SyntaxTree tree = CSharpSyntaxTree.ParseText(code, parseOptions);
                    CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

                    SyntaxNode strippedRoot = rewriter.Visit(root);

                    // 표준 포맷팅 및 정리
                    string outlineCode = strippedRoot.NormalizeWhitespace().ToFullString();

                    string relativePath = Path.GetRelativePath(inputDir, filePath);
                    string targetFilePath = Path.Combine(outputDir, relativePath);
                    string targetFileDir = Path.GetDirectoryName(targetFilePath)!;

                    if (!Directory.Exists(targetFileDir))
                    {
                        Directory.CreateDirectory(targetFileDir);
                    }

                    File.WriteAllText(targetFilePath, outlineCode, Encoding.UTF8);

                    long fileOutlineBytes = Encoding.UTF8.GetByteCount(outlineCode);
                    System.Threading.Interlocked.Add(ref outlineTotalBytes, fileOutlineBytes);
                    System.Threading.Interlocked.Increment(ref successCount);
                }
                catch (Exception ex)
                {
                    failureList.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                }
            });

            stopwatch.Stop();

            Console.WriteLine();
            Console.WriteLine("==================================================");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(" [SUCCESS] AI 아웃라인 생성 완료!");
            Console.ResetColor();
            Console.WriteLine($" - 처리 파일: {successCount} / {csFiles.Length}개 성공");
            Console.WriteLine($" - 소요 시간: {stopwatch.ElapsedMilliseconds} ms ({stopwatch.Elapsed.TotalSeconds:F2}초)");
            Console.WriteLine($" - 원본 총 크기: {originalTotalBytes:N0} bytes ({originalTotalBytes / 1024.0 / 1024.0:F2} MB)");
            Console.WriteLine($" - 아웃라인 크기: {outlineTotalBytes:N0} bytes ({outlineTotalBytes / 1024.0 / 1024.0:F2} MB)");

            double reduction = originalTotalBytes > 0
                ? (1.0 - ((double)outlineTotalBytes / originalTotalBytes)) * 100.0
                : 0.0;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($" - 토큰/용량 절감율: {reduction:F1}% 감소 (토큰 대폭 절약)");
            Console.ResetColor();
            Console.WriteLine($" - 저장 위치: {outputDir}");
            Console.WriteLine("==================================================");

            if (!failureList.IsEmpty)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARN] 처리 중 오류가 발생한 파일 ({failureList.Count}개):");
                foreach (var err in failureList.Take(10))
                {
                    Console.WriteLine($"  - {err}");
                }
                Console.ResetColor();
            }

            return 0;
        }
    }

    /// <summary>
    /// Roslyn 구문 트리를 순회하며 메서드/생성자/프로퍼티의 내부 구현 바디를 제거하고
    /// 클래스/인터페이스/필드/프로퍼티/메서드의 시그니처만 남기는 리라이터.
    /// </summary>
    internal sealed class OutlineSyntaxRewriter : CSharpSyntaxRewriter
    {
        private static readonly SyntaxToken Semicolon = SyntaxFactory.Token(SyntaxKind.SemicolonToken);

        // 메서드: 구현 바디({ ... }) 및 화살표 식(=> expr) 제거 후 세미콜론 부여
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var stripped = node
                .WithBody(null)
                .WithExpressionBody(null)
                .WithSemicolonToken(Semicolon);

            return base.VisitMethodDeclaration(stripped);
        }

        // 생성자: 바디 제거 후 세미콜론 부여 (베이스 생성자 호출 : base(...) 등은 시그니처로 보존)
        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            var stripped = node
                .WithBody(null)
                .WithExpressionBody(null)
                .WithSemicolonToken(Semicolon);

            return base.VisitConstructorDeclaration(stripped);
        }

        // 소멸자/파이널라이저: 바디 제거
        public override SyntaxNode? VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            var stripped = node
                .WithBody(null)
                .WithExpressionBody(null)
                .WithSemicolonToken(Semicolon);

            return base.VisitDestructorDeclaration(stripped);
        }

        // 프로퍼티: 식 바디 제거 및 게터/세터 바디 제거
        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            // 화살표 식 프로퍼티 (public int X => _x;) -> public int X { get; }
            if (node.ExpressionBody != null)
            {
                var getter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(Semicolon);

                var accessorList = SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(getter));

                var rewritten = node
                    .WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithAccessorList(accessorList);

                return base.VisitPropertyDeclaration(rewritten);
            }

            // 블록 접근자 (get { ... } set { ... }) -> get; set;
            if (node.AccessorList != null)
            {
                var accessors = node.AccessorList.Accessors.Select(a =>
                    a.WithBody(null)
                     .WithExpressionBody(null)
                     .WithSemicolonToken(Semicolon)
                );

                var rewritten = node.WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)));
                return base.VisitPropertyDeclaration(rewritten);
            }

            return base.VisitPropertyDeclaration(node);
        }

        // 인덱서: 프로퍼티와 동일하게 바디 제거
        public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            if (node.ExpressionBody != null)
            {
                var getter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(Semicolon);

                var rewritten = node
                    .WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(getter)));

                return base.VisitIndexerDeclaration(rewritten);
            }

            if (node.AccessorList != null)
            {
                var accessors = node.AccessorList.Accessors.Select(a =>
                    a.WithBody(null)
                     .WithExpressionBody(null)
                     .WithSemicolonToken(Semicolon)
                );

                var rewritten = node.WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)));
                return base.VisitIndexerDeclaration(rewritten);
            }

            return base.VisitIndexerDeclaration(node);
        }

        // 커스텀 이벤트 바디(add { } remove { }) 제거
        public override SyntaxNode? VisitEventDeclaration(EventDeclarationSyntax node)
        {
            if (node.AccessorList != null)
            {
                var accessors = node.AccessorList.Accessors.Select(a =>
                    a.WithBody(null)
                     .WithExpressionBody(null)
                     .WithSemicolonToken(Semicolon)
                );

                var rewritten = node.WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)));
                return base.VisitEventDeclaration(rewritten);
            }

            return base.VisitEventDeclaration(node);
        }

        // 연산자 오버로딩: 바디 제거
        public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            var stripped = node
                .WithBody(null)
                .WithExpressionBody(null)
                .WithSemicolonToken(Semicolon);

            return base.VisitOperatorDeclaration(stripped);
        }

        // 형변환 연산자: 바디 제거
        public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            var stripped = node
                .WithBody(null)
                .WithExpressionBody(null)
                .WithSemicolonToken(Semicolon);

            return base.VisitConversionOperatorDeclaration(stripped);
        }

        // 디컴파일 시 생성된 불필요한 컴파일러 전용 어트리뷰트 정리 (토큰 절약)
        public override SyntaxNode? VisitAttributeList(AttributeListSyntax node)
        {
            var filteredAttributes = node.Attributes.Where(attr =>
            {
                string name = attr.Name.ToString();
                return !name.Contains("CompilerGenerated") &&
                       !name.Contains("NullableContext") &&
                       !name.Contains("Nullable") &&
                       !name.Contains("DebuggerHidden") &&
                       !name.Contains("Embedded");
            }).ToList();

            if (filteredAttributes.Count == 0)
                return null; // 어트리뷰트 리스트 전체 제거

            return node.WithAttributes(SyntaxFactory.SeparatedList(filteredAttributes));
        }
    }
}
