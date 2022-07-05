using Discord.Commands;
using Discord.WebSocket;
using SpiixBot.Attributes;
using SpiixBot.Util;
using Sudoku.Solver.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpiixBot.Modules
{
    [Group("sudoku")]
    public class SudokuModule : ModuleBase<SocketCommandContext>
    {
        private string GetAsTable(string gameString)
        {
            char[][] table = new char[9][]
            {
                new char[9],
                new char[9],
                new char[9],
                new char[9],
                new char[9],
                new char[9],
                new char[9],
                new char[9],
                new char[9],
            };

            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 9; x++)
                {
                    table[y][x] = gameString[y * 9 + x];
                }
            }

            string[] rows = new string[19];
            rows[0] = "+---+---+---+---+---+---+---+---+---+";

            for (int i = 0; i < 9; i++)
            {
                rows[i * 2 + 1] = "| " + string.Join(" | ", table[i]) + " |";
                rows[i * 2 + 2] = "+---+---+---+---+---+---+---+---+---+";
            }

            return string.Join("\n", rows);
        }

        private const string _identifierSummary = "The game identifier. Usually looks like a string of 81 digits ranging from 0-9.";

        [Command("solve", RunMode = RunMode.Async)]
        [Summary("Solves a sudoku given a game identifier.")]
        public async Task SolveCommand([Summary(_identifierSummary)]string identifier)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(20000);

            try
            {
                var watch = Stopwatch.StartNew();
                SolveResult result = Solver.SolveByStringIdentifier(identifier, cts.Token);
                watch.Stop();

                if (!result.Success)
                {
                    if (result.Reason == InvalidReason.NoUniqueSolution) await ReplyAsync($"The entered puzzle is not solvable ({watch.ElapsedMilliseconds / 1000.0}s)");
                    else if (result.Reason == InvalidReason.InvalidLength) await ReplyAsync($"The entered puzzle must have 81 numbers ({watch.ElapsedMilliseconds / 1000.0}s)");
                    else if (result.Reason == InvalidReason.OverlappingValues) await ReplyAsync($"The entered puzzle has overlapping numbers ({watch.ElapsedMilliseconds / 1000.0}s)");
                }
                else await ReplyAsync($"Solution ({watch.ElapsedMilliseconds / 1000.0}s):```\n{GetAsTable(result.Solution)}``````\n{result.Solution}```");

            }
            catch (OperationCanceledException)
            {
                await ReplyAsync("The sudoku solver timed out.");
            }
        }
    }
}
