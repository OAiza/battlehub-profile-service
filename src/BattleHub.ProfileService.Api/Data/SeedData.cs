using BattleHub.ProfileService.Api.Domain.Entities;

namespace BattleHub.ProfileService.Api.Data;

/// <summary>
/// Datos iniciales del servicio: catálogo de juegos y permisos por defecto.
/// Se aplican con <c>HasData</c>, es decir, viajan dentro de la migración y el CI los obtiene
/// al correr <c>dotnet ef database update</c>, sin scripts manuales.
/// Los códigos provienen de <c>03-contratos-tecnicos.md</c> de battlehub-contracts.
/// </summary>
public static class SeedData
{
    /// <summary>Códigos de permiso definidos en el contrato.</summary>
    public static class PermissionCodes
    {
        public const string MatchesCreate = "matches.create";
        public const string TypingPlay = "games.typing.play";
        public const string TriviaPlay = "games.trivia.play";
        public const string MemoryPlay = "games.memory.play";
    }

    /// <summary>Códigos de juego; coinciden con el <c>gameType</c> del contrato.</summary>
    public static class GameCodes
    {
        public const string Typing = "typing";
        public const string Trivia = "trivia";
        public const string Memory = "memory";
    }

    /// <summary>
    /// Permisos por defecto. En esta etapa los cuatro se conceden a todo perfil nuevo;
    /// restringir un permiso es cambiar su <c>GrantedByDefault</c> a <c>false</c>.
    /// </summary>
    public static IReadOnlyList<Permission> Permissions { get; } =
    [
        new Permission
        {
            Code = PermissionCodes.MatchesCreate,
            Description = "Crear partidas en el lobby de Matchmaking",
            GrantedByDefault = true
        },
        new Permission
        {
            Code = PermissionCodes.TypingPlay,
            Description = "Jugar Typing Battle",
            GrantedByDefault = true
        },
        new Permission
        {
            Code = PermissionCodes.TriviaPlay,
            Description = "Jugar Trivia Battle",
            GrantedByDefault = true
        },
        new Permission
        {
            Code = PermissionCodes.MemoryPlay,
            Description = "Jugar Memory Match",
            GrantedByDefault = true
        }
    ];

    /// <summary>Catálogo de los tres juegos del proyecto (Equipos 4, 5 y 6).</summary>
    public static IReadOnlyList<Game> Games { get; } =
    [
        new Game
        {
            Code = GameCodes.Typing,
            Name = "Typing Battle",
            Description = "Competencia de velocidad y precisión de escritura.",
            RequiredPermissionCode = PermissionCodes.TypingPlay,
            IsActive = true,
            SortOrder = 1
        },
        new Game
        {
            Code = GameCodes.Trivia,
            Name = "Trivia Battle",
            Description = "Preguntas y respuestas por categorías contra otros jugadores.",
            RequiredPermissionCode = PermissionCodes.TriviaPlay,
            IsActive = true,
            SortOrder = 2
        },
        new Game
        {
            Code = GameCodes.Memory,
            Name = "Memory Match",
            Description = "Juego de memoria: encontrar los pares en el menor tiempo posible.",
            RequiredPermissionCode = PermissionCodes.MemoryPlay,
            IsActive = true,
            SortOrder = 3
        }
    ];
}
