# AzertyFix — MECCHA CHAMELEON en AZERTY

**Développé par Ale2x_**

MECCHA CHAMELEON est câblé en QWERTY et n'a **aucun menu de remappage clavier**. Sur un clavier AZERTY, WASD tombe n'importe où et le jeu est injouable. AzertyFix règle ça.

### ➜ [Télécharger la dernière version](https://github.com/Ale2x34/AzertyFix/releases/latest)

Rien à installer, aucune dépendance. Décompresse, lance `AzertyFix.exe`.

## Ce que ça fait

L'outil inverse deux paires de touches :

```
A <-> Q          et          Z <-> W
```

**Uniquement quand la fenêtre du jeu est au premier plan.** Dès que tu passes sur une autre application, ton AZERTY redevient parfaitement normal. Aucun fichier du jeu n'est modifié, aucun réglage clavier de Windows non plus.

### En jeu

| Tu appuies sur | Le jeu reçoit | Action |
|---|---|---|
| **Z** | W | Avancer |
| **Q** | A | Gauche |
| **S** | S | Reculer |
| **D** | D | Droite |
| **A** | Q | Release / Copy Create |

Toutes les autres touches (`E F R C V X G Y B T`, `1`, Espace, Maj, Ctrl, Alt, Échap, Tab) sont déjà au même endroit physique sur AZERTY et sur QWERTY : elles ne sont pas touchées.

## Installation

Rien à installer. Télécharge, décompresse où tu veux, lance `AzertyFix.exe`. Une icône apparaît à côté de l'horloge. Tu peux le lancer avant ou pendant une partie.

| Touche | Effet |
|---|---|
| **F9** | Active / désactive le remap à la volée — pratique pour taper dans le chat du jeu en vrai AZERTY |
| **F10** | Ouvre le journal de diagnostic |

Clic droit sur l'icône pour le mode scancode, le lancement automatique avec Windows, le journal, « À propos » et quitter.

## « Windows a protégé votre ordinateur »

C'est attendu. Le programme n'est pas signé numériquement, et il utilise un *hook clavier bas niveau* — la même technique qu'un logiciel de macros, mais aussi celle d'un keylogger. Les antivirus sont méfiants par principe, sans distinguer les deux usages.

Pour lancer quand même : **Informations complémentaires → Exécuter quand même.**

Si tu n'as pas confiance — réaction saine —, le code source complet est là, dans `AzertyFix.cs`. Tu peux le lire et le recompiler toi-même pour obtenir un exécutable dont tu es certain du contenu (voir plus bas).

## Vie privée

- **Rien n'est envoyé sur Internet.** Il n'y a aucun code réseau dans la source, tu peux vérifier.
- **Seules 4 touches sont lues** : A, Q, Z et W. Toutes les autres frappes traversent le programme sans être ni examinées ni enregistrées.
- **Le journal de diagnostic est désactivé par défaut.** Activé, il reste sur ton disque et ne note **jamais** le nom ni le titre des fenêtres autres que le jeu : rien de ce que tu fais ailleurs sur ton PC n'y apparaît.
- Seules traces sur le système : `HKCU\Software\Ale2x_\AzertyFix` pour tes réglages, et l'entrée de démarrage automatique si tu l'actives.

## Si ça ne marche pas

1. Vérifie que « Remap actif » est coché (clic droit sur l'icône).
2. Active « Journal de diagnostic », retourne en jeu, appuie sur ZQSD, puis rouvre le journal avec **F10**. Il dit si la fenêtre du jeu est reconnue et si les touches sont envoyées.
3. Le journal dit « envoie » mais rien ne bouge ? Coche **« Mode scancode »** : certains moteurs ignorent les touches virtuelles et ne lisent que les codes physiques.
4. Le journal dit « SendInput refusé » ? Le jeu tourne en administrateur et pas AzertyFix — lance AzertyFix en administrateur aussi.

## Compiler soi-même

Aucun outil à télécharger, le compilateur C# est déjà fourni avec Windows :

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo ^
  -target:winexe -optimize+ -out:AzertyFix.exe -r:System.dll ^
  -r:System.Drawing.dll -r:System.Windows.Forms.dll AzertyFix.cs
```

`AzertyFix.cs` doit garder son BOM UTF-8, sinon les accents sont cassés à la compilation.

## Comment ça marche

Un hook clavier bas niveau (`WH_KEYBOARD_LL`) intercepte les quatre touches concernées. Quand la fenêtre active appartient au jeu, la touche d'origine est bloquée et sa contrepartie QWERTY est réinjectée via `SendInput`. Les événements injectés sont marqués pour ne pas être retraités.

Un détail qui m'a fait perdre du temps et qui peut servir à d'autres : **le nom du processus n'est pas celui de l'exécutable lancé.** `PenguinHotel.exe` n'est qu'un lanceur — le jeu tourne en réalité dans `PenguinHotel-Win64-Shipping.exe`, avec une fenêtre titrée « Chameleon ». Les deux sont reconnus, plus un filet de sécurité sur le titre de la fenêtre au cas où le binaire serait renommé lors d'une mise à jour.

## Licence

**Tous droits réservés** — voir [`LICENSE.txt`](LICENSE.txt).

Le programme est gratuit, tu peux l'utiliser, le lire et le compiler pour toi. En revanche il n'est **pas** sous licence libre : pas de redistribution, de republication, de version modifiée ni d'usage commercial sans mon accord écrit.

---

MECCHA CHAMELEON appartient à ses auteurs. AzertyFix n'est ni affilié ni approuvé par eux, et ne modifie aucun fichier du jeu.
