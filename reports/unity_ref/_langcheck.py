import io, re, glob, collections
tot = collections.Counter()
for p in glob.glob('F:/GithubPro/Darkest-Dungeon-Unity/Assets/Resources/Data/Localization/*.xml'):
    d = io.open(p, encoding='utf-8', errors='replace').read()
    for c in d.split('<language id="')[1:]:
        lid = c.split('"', 1)[0]
        tot[lid] += len(re.findall(r'<entry\b', c))
print(tot.most_common())
print('sum', sum(tot.values()))
