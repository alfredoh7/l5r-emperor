from __future__ import annotations
import json
from pathlib import Path
from package_vanilla_wave import build_definition_fields
ROOT = Path(__file__).resolve().parents[2]
PROFILE = 'personality-wave-9701'
CARDS = [(10837, 'aftermath', [], 'none', {}), (10868, 'aftermath', ['w9701-jin-e'], 'none', {}), (10864, 'aftermath', [], 'none', {}), (10284, 'torn-asunder', [], 'none', {}), (10666, 'gates-of-chaos', [], 'enters-bowed', {})]
CARD_IDS = ['oracle-l5r-' + str(row[0]) for row in CARDS]
def package(card_ids=CARD_IDS, root=ROOT):
    if card_ids != CARD_IDS: raise ValueError('This wave uses its reviewed card list')
    cards, sources = [], []
    for number, slug, abilities, effect, extra in CARDS:
        card_id = 'oracle-l5r-' + str(number)
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        fields.pop('_oracle_id')
        assert '/oracle/' + slug + '/' in source['image_path'], card_id
        expected_set = {'coils-of-madness': 'Coils of Madness', 'torn-asunder': 'Torn Asunder', 'aftermath': 'Aftermath', 'gates-of-chaos': 'Gates of Chaos'}[slug]
        assert source['printing_set'] == [expected_set], (card_id, source['printing_set'])
        fields.update(Abilities=abilities, Effect=effect)
        fields.update(extra)
        cards.append(fields); sources.append(source)
    assert len(cards) == len(set(c['Id'] for c in cards))
    path = root / 'content/published'; path.mkdir(parents=True, exist_ok=True)
    (path / (PROFILE + '.json')).write_text(json.dumps(dict(Profile=PROFILE, Rules='Emperor Edition; reviewed period printings; issue #689.', Cards=cards, Decks=[]), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (path / (PROFILE + '-sources.json')).write_text(json.dumps(sources, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return {'cards': len(cards)}
if __name__ == '__main__': print(json.dumps(package()))
