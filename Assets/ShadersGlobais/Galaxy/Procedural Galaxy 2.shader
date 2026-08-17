Shader "Custom/ProceduralUndergroundGuardian"
{
    Properties
    {
        [Header(Underground Colors)]
        _AbyssColor ("Deep Abyss (Background)", Color) = (0.2, 0.05, 0.1, 1.0)
        _CoreGlowColor ("Core Heat Glow", Color) = (1.0, 0.1, 0.6, 1.0)
        
        [Header(Mana Vapors (Nebula))]
        _NebulaColor ("Mana Vapors (Cyan)", Color) = (0.1, 0.8, 0.9, 1.0)
        _NebulaSpeed ("Vapor Speed", Float) = 0.08
        _NebulaDirection ("Vapor Direction (X,Y)", Vector) = (0.0, -1.0, 0.0, 0.0)
        _NebulaScale ("Vapor Scale", Float) = 2.0

        [Header(Magical Embers (Stars))]
        _StarColor ("Ember Gold", Color) = (1.0, 0.9, 0.4, 1.0) 
        _EmberRiseSpeed ("Ember Speed", Float) = 0.1
        _EmberDirection ("Ember Direction (X,Y)", Vector) = (0.0, 1.0, 0.0, 0.0)
        _EmberScale ("Ember Grid Scale", Float) = 12.0

        [Header(Shooting Stars)]
        _ShootingStarColor ("Shooting Star Color", Color) = (1.0, 0.8, 1.0, 1.0)
        _ShootingStarSpeed ("Shooting Star Speed", Float) = 2.0
        _ShootingStarDirection ("Shooting Star Direction (X,Y)", Vector) = (-1.0, -1.0, 0.0, 0.0)
        _ShootingStarDensity ("Density (0 to 1)", Range(0, 1)) = 0.8
        _ShootingStarScale ("Shooting Star Grid Scale", Float) = 3.0
        _ShootingStarTrail ("Trail Length", Float) = 0.4

        [Header(Movement and Twinkle)]
        _TwinkleSpeed ("Twinkle Speed", Float) = 3.0
        _TwinkleMin ("Twinkle Min", Range(0, 1)) = 0.1
        _TwinkleMax ("Twinkle Max", Range(1, 5)) = 3.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            // Cores e Fundo
            float4 _AbyssColor;
            float4 _CoreGlowColor;
            
            // Névoa
            float4 _NebulaColor;
            float _NebulaSpeed;
            float4 _NebulaDirection;
            float _NebulaScale;

            // Brasas (Estrelas)
            float4 _StarColor;
            float _EmberRiseSpeed;
            float4 _EmberDirection;
            float _EmberScale;

            // Estrelas Cadentes
            float4 _ShootingStarColor;
            float _ShootingStarSpeed;
            float4 _ShootingStarDirection;
            float _ShootingStarDensity;
            float _ShootingStarScale;
            float _ShootingStarTrail;

            // Cintilação
            float _TwinkleSpeed;
            float _TwinkleMin;
            float _TwinkleMax;

            // Função de ruído aleatório
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Value Noise para a névoa
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // FBM (Gases mágicos subterrâneos)
            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                float2 shift = float2(100.0, 100.0);
                float2x2 rot = float2x2(cos(0.5), sin(0.5), -sin(0.5), cos(0.5));
                
                for (int i = 0; i < 5; ++i)
                {
                    v += a * noise(p);
                    p = mul(rot, p) * 2.0 + shift;
                    a *= 0.5;
                }
                return v;
            }

            // Estrelas em formato de cruz / Brasas mágicas
            float magicalEmbers(float2 uv)
            {
                float2 grid = floor(uv * _EmberScale); 
                float2 randomOffset = float2(hash21(grid), hash21(grid + 7.0)) * 0.6 + 0.2;
                float2 starPos = grid + randomOffset;
                
                float2 dist = (uv * _EmberScale) - starPos;
                float len = length(dist);
                
                float core = smoothstep(0.12, 0.0, len);
                
                // Brilho Star Guardian (Cruz)
                float glowX = smoothstep(0.03, 0.0, abs(dist.x)) * smoothstep(0.5, 0.0, abs(dist.y));
                float glowY = smoothstep(0.03, 0.0, abs(dist.y)) * smoothstep(0.5, 0.0, abs(dist.x));
                float crossGlow = glowX + glowY;
                
                float brightness = core + crossGlow;
                
                float timeVar = _Time.y * _TwinkleSpeed + hash21(grid) * 10.0;
                float twinkle = sin(timeVar) * 0.5 + 0.5; 
                brightness *= lerp(_TwinkleMin, _TwinkleMax, twinkle);
                
                if(hash21(grid + 33.3) > 0.4) 
                {
                    brightness = 0.0;
                }

                return brightness;
            }

            // Estrelas Cadentes (Shooting Stars) com direção dinâmica
            float shootingStars(float2 uv)
            {
                // Divide a tela baseado na escala customizada
                float2 grid = floor(uv * _ShootingStarScale);
                float2 localUV = frac(uv * _ShootingStarScale) - 0.5;
                
                // Semente única por célula
                float cellSeed = hash21(grid); 
                
                // Sincroniza o tempo para que a estrela percorra a célula e resete
                float t = _Time.y * _ShootingStarSpeed + cellSeed * 100.0;
                float tFrac = frac(t);
                float tFloor = floor(t);
                
                // Rola o dado novamente a cada ciclo
                float spawnChance = hash21(grid + tFloor);
                if(spawnChance < _ShootingStarDensity) return 0.0;
                
                // Direção customizável e normalizada
                float2 dir = normalize(_ShootingStarDirection.xy);
                
                // O start e end se ajustam baseados na direção desejada (0.707 garante que cruze a célula inteira)
                float2 startPos = -dir * 0.707;
                float2 endPos = dir * 0.707;
                
                // Posição atual baseada no tempo
                float2 currentPos = lerp(startPos, endPos, tFrac);
                
                // Vetor do pixel atual até a posição da estrela
                float2 toPixel = localUV - currentPos;
                
                // Criação do rastro projetando o vetor na direção inversa usando o comprimento customizado
                float h = clamp(dot(toPixel, -dir) / _ShootingStarTrail, 0.0, 1.0); 
                
                // Calcula a distância do pixel até o segmento de linha do rastro
                float distToLine = length(toPixel - (-dir) * h);
                
                // A espessura da estrela afina da cabeça (0.02) para a cauda (0.0)
                float thickness = lerp(0.02, 0.0, h); 
                float star = smoothstep(thickness, 0.0, distToLine);
                
                // Suaviza a entrada e a saída (Fade in / Fade out) nas bordas da célula
                star *= smoothstep(0.0, 0.1, tFrac) * smoothstep(1.0, 0.8, tFrac);
                
                return star;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                
                // 1. FUNDO DO ABISMO E NÚCLEO (Gradient Base)
                float coreHeat = smoothstep(0.7, -0.2, uv.y);
                float4 backgroundColor = lerp(_AbyssColor, _CoreGlowColor, coreHeat * 0.8);

                // 2. NÉVOA / MANA VAPORS
                // Movimento da névoa baseado na nova direção e escala
                float2 nebulaUV = uv * _NebulaScale;
                float2 nebulaMove = _Time.y * _NebulaSpeed * _NebulaDirection.xy;
                float n = fbm(nebulaUV + nebulaMove);
                float n2 = fbm(nebulaUV * 2.0 - nebulaMove * 0.5);
                
                // Combina a cor do núcleo com o ciano da mana
                float4 vaporColor = lerp(_CoreGlowColor, _NebulaColor, n);
                vaporColor *= pow(n2, 1.5) * 1.5; 
                
                // 3. BRASAS MÁGICAS (Estrelas Subindo)
                // Movimentação livre baseada em direção no Inspector
                float2 emberUV = uv;
                emberUV -= _Time.y * _EmberRiseSpeed * _EmberDirection.xy; 
                
                float embers1 = magicalEmbers(emberUV);
                // A segunda camada se move mais rápido e é maior (efeito parallax padrão)
                float embers2 = magicalEmbers((emberUV + float2(0.5, 0.5)) * 1.5) * 0.5;
                float totalEmbers = embers1 + embers2;

                // 4. ESTRELAS CADENTES
                float fallingStars = shootingStars(uv);

                // 5. COMPOSIÇÃO FINAL
                float4 finalColor = backgroundColor;
                finalColor += vaporColor;
                finalColor += totalEmbers * _StarColor;
                finalColor += fallingStars * _ShootingStarColor * 2.0; 
                
                return finalColor;
            }
            ENDCG
        }
    }
}