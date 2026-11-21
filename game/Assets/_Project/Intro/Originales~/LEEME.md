# Originales de la intro en cómic

Pon aquí las imágenes tal como salen de Gemini, con el nombre que tengan.

- **Unity ignora esta carpeta** porque su nombre termina en `~`: no crea `.meta` ni mete las imágenes en el juego.
- **Git también la ignora** (`.gitignore`): los originales pesados no se suben a GitHub.

Después, con la lista de prompts en orden, se renombran y se convierten a JPG 16:9 de 1-2 MB como máximo
y se copian a `../Resources/intro_01.jpg`, `intro_02.jpg`... Ahí las lee la intro del juego
(`Scripts/Intro/IntroSequence.cs`), una imagen por línea de `../Resources/intro.txt`.
