"""Adversarial checks plus UI/rules rehearsals against real temporary Git repos."""
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('collab', REPO / 'tools/collab.py')
c = importlib.util.module_from_spec(spec)
spec.loader.exec_module(c)
CFG = c.config(REPO)


class PolicyTests(unittest.TestCase):
    def test_role_routing(self):
        for paths, lane in [(['src/L5R.Client/CardTile.cs'], 'ui'),
                            (['src/L5R.Core/CardAbilities.cs'], 'rules'),
                            (['src/L5R.Core/Models.cs'], 'shared'),
                            (['src/L5R.Client/Main.Actions.cs'], 'shared'),
                            (['tools/check.sh'], 'shared'),
                            (['unknown/new-file.cs'], 'shared'),
                            (['src/L5R.Client/CardTile.cs', 'content/published/deck.json'], 'shared')]:
            with self.subTest(paths=paths):
                self.assertEqual(c.classify(paths, CFG), lane)

    def test_handoff_does_not_add_a_second_lane(self):
        self.assertEqual(c.classify(['src/L5R.Client/CardTile.cs', CFG['handoffs']+'task.json'],CFG),'ui')

    def test_reviewers_exclude_author_case_insensitively(self):
        self.assertEqual(c.reviewers('ui', 'ALFREDOH7', CFG), [])
        self.assertEqual(c.reviewers('rules', 'alfredoh7', CFG), ['mdornbrook'])
        self.assertEqual(c.reviewers('shared', 'mdornbrook', CFG), [])

    def test_mendon_can_work_in_every_lane_without_an_owner_review_gate(self):
        for lane in ('ui', 'rules', 'shared'):
            with self.subTest(lane=lane):
                self.assertEqual(c.reviewers(lane, 'MDORNBROOK', CFG), [])
        self.assertEqual(c.reviewers('shared', 'alfredoh7', CFG), ['mdornbrook'])
        self.assertEqual(c.reviewers('ui', 'unlisted-contributor', CFG), ['alfredoh7'])
        self.assertEqual(c.reviewers('shared', 'mdornbrook-impostor', CFG), ['alfredoh7', 'mdornbrook'])
        self.assertFalse(c.autonomous_developer(None, CFG))

    def test_legacy_policy_does_not_implicitly_grant_autonomy(self):
        legacy = {k:v for k,v in CFG.items() if k != 'autonomous_developers'}
        self.assertEqual(c.reviewers('shared', 'mdornbrook', legacy), ['alfredoh7'])
        self.assertFalse(c.autonomous_developer('mdornbrook', legacy))

    def test_handoff_names_cannot_escape_or_collide_after_slugging(self):
        names=['../../outside', 'a/b', 'a-b', '$(touch hacked)', 'x'*250]
        paths=[c.handoff_path(n,CFG) for n in names]
        self.assertEqual(len(set(paths)),len(paths))
        for p in paths:
            self.assertNotIn('..', p)
            self.assertTrue(p.startswith(CFG['handoffs']))
            self.assertLess(len(Path(p).name),100)

    def test_placeholder_wrong_lane_and_wrong_task_rejected(self):
        record={'lane':'ui','summary':'placeholder','validation':'todo','remaining':'' ,'branch':'other'}
        errors=c.validate_record(record,'shared','task')
        self.assertEqual(len(errors),5)

    def test_non_object_handoff_is_rejected_without_crashing(self):
        for value in ([], 3, None, "fake record"):
            self.assertTrue(c.validate_record(value, 'ui'))

    def test_honest_unperformed_validation_is_allowed(self):
        record={'lane':'ui','summary':'Changed hover placement.', 'validation':'Not run: machine has no graphical display.', 'remaining':'Native visual review is still required.'}
        self.assertFalse(c.validate_record(record,'ui'))

    def test_claude_imports_one_shared_policy(self):
        self.assertIn('@AGENTS.md', (REPO/'CLAUDE.md').read_text())
        self.assertLess(len((REPO/'AGENTS.md').read_bytes()),12000)
        for term in ('collab.py context','collab.py handoff','collab.py audit','alfredoh7','mdornbrook'):
            self.assertIn(term,(REPO/'AGENTS.md').read_text())

    def test_router_never_checks_out_head_or_interpolates_task_text(self):
        source=(REPO/'.github/workflows/review-routing.yml').read_text()
        self.assertIn('ref: ${{ github.event.pull_request.base.sha }}',source)
        self.assertNotIn('pull_request.head',source)
        self.assertNotIn('github.head_ref',source)
        self.assertNotIn('pull_request.title',source)
        self.assertIn('persist-credentials: false',source)
        self.assertIn("if: github.event.pull_request.base.ref == 'main'",source)

    def test_run_uses_argument_arrays_no_shell(self):
        with patch.object(c.subprocess,'run') as fake:
            fake.return_value=subprocess.CompletedProcess([],0,'safe','')
            c.run(['git','show','$(touch hacked)'],REPO)
            args,kwargs=fake.call_args
            self.assertEqual(args[0][-1],'$(touch hacked)')
            self.assertNotIn('shell',kwargs)


class GitRehearsals(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory(prefix='l5r-collab-test-')
        self.root=Path(self.tmp.name)/'project with spaces'
        self.root.mkdir()
        self.git('init','-b','main')
        self.git('config','user.name','Workflow test')
        self.git('config','user.email','workflow@example.invalid')
        self.git('config','commit.gpgsign','false')
        self.git('config','core.autocrlf','false')
        self.write('.github/collaboration.json',json.dumps(CFG))
        self.write('src/L5R.Client/CardTile.cs','original UI\n')
        self.write('src/L5R.Core/CardAbilities.cs','original rules\n')
        self.write('src/L5R.Core/Models.cs','shared types\n')
        self.save('baseline')
        self.git('update-ref','refs/remotes/origin/main','HEAD')

    def tearDown(self):
        # Windows read-only Git files need chmod when cleaning temporary fixtures.
        def writable_then_remove(func,path,exc):
            Path(path).chmod(0o700)
            func(path)
        shutil.rmtree(self.tmp.name,onerror=writable_then_remove)
        self.tmp.cleanup()

    def git(self,*args):
        return c.git(self.root,*args)

    def write(self,path,text):
        p=self.root/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text,encoding='utf-8')

    def save(self,message):
        self.git('add','.');self.git('-c','gc.auto=0','commit','-m',message)

    def record(self,lane,name=None):
        name=name or c.branch(self.root)
        self.write(c.handoff_path(name,CFG),json.dumps({'branch':name,'lane':lane,'summary':'A representative task change.', 'validation':'Fixture verification completed successfully.', 'remaining':'None; ready for review.'}))

    def test_ui_task_full_local_rehearsal(self):
        self.git('switch','-c','codex/10-card-hover')
        self.write('src/L5R.Client/CardTile.cs','larger hover preview\n')
        result=c.context(self.root,offline=True)
        self.assertFalse(result['fresh_git']);self.assertTrue(result['working_tree'])
        self.record('ui')
        self.assertFalse(c.audit(self.root,'origin/main',name=c.branch(self.root),working=True)['errors'])
        self.save('UI task and handoff')
        result=c.audit(self.root,'origin/main',name=c.branch(self.root))
        self.assertEqual(result['lane'],'ui');self.assertEqual(result['errors'],[])

    def test_stacked_task_keeps_foreign_handoff_but_requires_own_shared_record(self):
        self.git('switch', '-c', 'claude/dependency')
        self.write('src/L5R.Core/CardAbilities.cs', 'dependency rules')
        self.record('rules'); self.save('dependency handoff')
        self.git('switch', '-c', 'codex/stacked-ui')
        self.write('src/L5R.Client/CardTile.cs', 'UI using dependency')
        report = c.audit(self.root, 'origin/main', name=c.branch(self.root), working=True)
        self.assertTrue(any('another branch' in e for e in report['errors']))
        self.record('ui')
        self.assertTrue(c.audit(self.root, 'origin/main', name=c.branch(self.root), working=True)['errors'])
        self.record('shared'); self.save('current shared handoff')
        self.assertEqual(c.audit(self.root, 'origin/main', name=c.branch(self.root))['errors'], [])

    def test_malformed_foreign_handoff_is_not_ignored(self):
        self.git('switch', '-c', 'codex/stacked-ui')
        self.record('ui')
        self.write(c.handoff_path('other', CFG), json.dumps({'branch': 'other', 'lane': 'ui', 'summary': 'todo'}))
        self.assertTrue(c.audit(self.root, 'origin/main', name=c.branch(self.root), working=True)['errors'])

    def test_rules_task_full_local_rehearsal(self):
        self.git('switch','-c','claude/11-ability-timing')
        self.write('src/L5R.Core/CardAbilities.cs','corrected ability timing\n')
        self.record('rules');self.save('Rules task and handoff')
        result=c.audit(self.root,'origin/main',name=c.branch(self.root))
        self.assertEqual(result['lane'],'rules');self.assertEqual(result['errors'],[])

    def test_mendon_custom_deck_menu_and_rules_rehearsal(self):
        self.git('switch', '-c', 'claude/custom-deck-playtest')
        self.write('src/L5R.Client/MainMenu.cs', 'custom deck practice entry point\n')
        self.write('src/L5R.Core/CardAbilities.cs', 'testable card mechanics\n')
        self.record('shared'); self.save('Mendon playtest task and handoff')
        result = c.audit(self.root, 'origin/main', name=c.branch(self.root))
        self.assertEqual(result['lane'], 'shared')
        self.assertEqual(result['errors'], [])
        self.assertEqual(c.reviewers(result['lane'], 'mdornbrook', CFG), [])
        self.assertEqual((self.root/'src/L5R.Client/CardTile.cs').read_text(), 'original UI\n')
        # The grant does not weaken freshness/branch/handoff validation; the
        # existing missing-handoff/stale-base/dirty-work tests continue to apply.

    def test_untracked_cross_lane_file_cannot_hide_from_audit(self):
        self.git('switch','-c','codex/ui-task');self.record('ui')
        self.write('src/L5R.Client/CardTile.cs','UI change')
        self.write('src/L5R.Core/new-rule.cs','untracked rules change')
        report=c.audit(self.root,'origin/main',name=c.branch(self.root),working=True)
        self.assertEqual(report['lane'],'shared');self.assertTrue(report['errors'])

    def test_rename_across_lanes_retains_both_paths(self):
        self.git('switch','-c','codex/move-card')
        self.git('mv','src/L5R.Core/CardAbilities.cs','src/L5R.Client/CardAbilities.cs')
        self.record('ui');self.save('move file')
        result=c.audit(self.root,'origin/main',name=c.branch(self.root))
        self.assertEqual(result['lane'],'shared');self.assertTrue(result['errors'])

    def test_deleted_shared_file_requires_shared_handoff(self):
        self.git('switch','-c','codex/delete-shared')
        self.git('rm','src/L5R.Core/Models.cs');self.record('ui');self.save('delete')
        self.assertEqual(c.audit(self.root,'origin/main')['lane'],'shared')
        self.assertTrue(c.audit(self.root,'origin/main')['errors'])

    def test_missing_handoff_rejected(self):
        self.git('switch','-c','codex/no-handoff');self.write('src/L5R.Client/CardTile.cs','changed');self.save('edit')
        self.assertTrue(c.audit(self.root,'origin/main')['errors'])

    def test_committed_audit_cannot_use_untracked_fake_handoff(self):
        self.git('switch','-c','codex/untracked-handoff');self.write('src/L5R.Client/CardTile.cs','changed');self.save('edit')
        self.record('ui')
        self.assertTrue(c.audit(self.root,'origin/main')['errors'])
        self.assertFalse(c.audit(self.root,'origin/main',working=True)['errors'])

    def test_stale_base_is_reported_without_editing_dirty_work(self):
        self.git('switch','-c','codex/ui-task');self.record('ui');self.save('UI handoff')
        self.git('switch','main');self.write('src/L5R.Core/CardAbilities.cs','new rules');self.save('friend rules')
        self.git('update-ref','refs/remotes/origin/main','HEAD')
        self.git('switch','codex/ui-task');self.write('src/L5R.Client/CardTile.cs','my unsaved change')
        before=self.git('status','--porcelain')
        result=c.audit(self.root,'origin/main',working=True)
        self.assertTrue(any('behind' in e for e in result['errors']))
        self.assertEqual(self.git('status','--porcelain'),before)
        self.assertEqual((self.root/'src/L5R.Client/CardTile.cs').read_text(),'my unsaved change')

    def test_unexpected_remote_is_not_fetched(self):
        self.git('remote','add','origin','https://example.invalid/unrelated.git')
        with patch.object(c,'gh',side_effect=RuntimeError('offline')):
            result=c.context(self.root)
        self.assertFalse(result['fresh_git']);self.assertTrue(any('differs' in x for x in result['warnings']))

    def test_context_reports_overlap_with_other_branch(self):
        self.git('switch','-c','codex/hover')
        self.write('src/L5R.Client/CardTile.cs','local hover edit')
        self.git('remote','add','origin',f'https://github.com/{CFG["repository"]}.git')
        real_git=c.git
        def fake_git(root,*args):
            return '' if args[:2]==('fetch','origin') else real_git(root,*args)
        def fake_gh(args,root):
            if args[:2]==['api','user']:return 'alfredoh7'
            if args[:2]==['pr','list']:
                return json.dumps([{'number':7,'url':'https://github.com/example/pull/7','headRefName':'claude/other','files':[{'path':'src/L5R.Client/CardTile.cs'}]}])
            return '[]'
        with patch.object(c,'git',side_effect=fake_git),patch.object(c,'gh',side_effect=fake_gh):
            report=c.context(self.root)
        self.assertEqual(report['overlapping_prs'][0]['number'],7)
        self.assertEqual(report['overlapping_prs'][0]['files'],['src/L5R.Client/CardTile.cs'])

    def test_two_lanes_push_independently_and_combine_cleanly(self):
        origin=Path(self.tmp.name)/'origin.git'
        friend=Path(self.tmp.name)/'friend clone'
        self.git('clone','--bare',str(self.root),str(origin))
        self.git('remote','add','origin',str(origin))
        self.git('clone',str(origin),str(friend))
        c.git(friend,'config','user.name','Rules rehearsal')
        c.git(friend,'config','user.email','rules@example.invalid')
        c.git(friend,'config','commit.gpgsign','false')
        self.git('switch','-c','codex/ui-rehearsal')
        self.write('src/L5R.Client/CardTile.cs','larger hover preview\n')
        self.record('ui');self.save('UI task');self.git('push','origin','HEAD')
        c.git(friend,'switch','-c','claude/rules-rehearsal')
        (friend/'src/L5R.Core/CardAbilities.cs').write_text('corrected timing\n')
        path=friend/c.handoff_path('claude/rules-rehearsal',CFG);path.parent.mkdir(parents=True,exist_ok=True)
        path.write_text(json.dumps({'branch':'claude/rules-rehearsal','lane':'rules','summary':'Correct the timing behavior.','validation':'Regression rehearsal passed.','remaining':'None; ready for review.'}))
        c.git(friend,'add','.');c.git(friend,'commit','-m','Rules task');c.git(friend,'push','origin','HEAD')
        self.git('fetch','origin')
        self.assertEqual(c.audit(self.root,'origin/main',name='codex/ui-rehearsal')['errors'],[])
        self.assertEqual(c.audit(friend,'origin/main',name='claude/rules-rehearsal')['errors'],[])
        self.git('switch','main');self.git('merge','--no-edit','codex/ui-rehearsal')
        self.git('merge','--no-edit','origin/claude/rules-rehearsal')
        self.assertEqual((self.root/'src/L5R.Client/CardTile.cs').read_text(),'larger hover preview\n')
        self.assertEqual((self.root/'src/L5R.Core/CardAbilities.cs').read_text(),'corrected timing\n')

    def test_detached_head_context_is_readable(self):
        self.git('checkout','--detach')
        self.assertIn('detached',c.context(self.root,offline=True)['branch'])

    def test_main_branch_is_rejected(self):
        self.record('ui');self.write('src/L5R.Client/CardTile.cs','changed');self.save('bad direct edit')
        self.assertTrue(any('task branch' in e for e in c.audit(self.root,'origin/main',name='main')['errors']))

    def test_live_context_resolves_both_roles_without_mutating_files(self):
        self.git('remote','add','origin',f'https://github.com/{CFG["repository"]}.git')
        real_git=c.git
        def fake_git(root,*args):
            return '' if args[:2]==('fetch','origin') else real_git(root,*args)
        for login,lane in CFG['owners'].items():
            # Mapping is lane -> login in the configuration.
            def fake_gh(args,root):
                return lane if args[:2]==['api','user'] else '[]'
            with patch.object(c,'git',side_effect=fake_git),patch.object(c,'gh',side_effect=fake_gh):
                result=c.context(self.root)
            self.assertEqual(result['default_lane'],login)
            self.assertEqual(result['autonomous_development'], lane == 'mdornbrook')
            self.assertEqual(result['autonomous_developers'], ['mdornbrook'])
            self.assertIn('approval_policy', result)
            self.assertTrue(result['fresh_github']);self.assertTrue(result['fresh_git'])
            self.assertEqual(self.git('status','--porcelain'),'')


class RouterTests(unittest.TestCase):
    def exercise(self,files,draft=False,author='alfredoh7',requested=(),reviews=(),changed=None,metadata=None):
        mutations=[]
        def fake(args,root):
            route=next((x for x in args if x.startswith('repos/')),'')
            if '--method' in args:
                mutations.append(args);return '{}'
            if route.endswith('/files?per_page=100'):return json.dumps([files])
            if route.endswith('/reviews?per_page=100'):return json.dumps([reviews])
            if route.endswith('/labels'):return '[]'
            return json.dumps({'changed_files':len(files) if changed is None else changed,'draft':draft,'user':{'login':author},'requested_reviewers':[{'login':x} for x in requested], **(metadata or {})})
        with patch.object(c,'gh',side_effect=fake):
            result=c.route_pr(REPO,12)
        return result,mutations

    def test_shared_route_requests_other_lead(self):
        result,calls=self.exercise([{'filename':'src/L5R.Core/Models.cs'}])
        self.assertEqual(result['lane'],'shared')
        self.assertTrue(any('reviewers[]=mdornbrook' in x for x in calls))

    def test_mendon_menu_and_shared_prs_get_labels_without_review_requests(self):
        for files in ([{'filename':'src/L5R.Client/MainMenu.cs'}],
                      [{'filename':'src/L5R.Client/Main.cs'}, {'filename':'src/L5R.Core/CardAbilities.cs'}]):
            with self.subTest(files=files):
                result,calls = self.exercise(files, author='mdornbrook')
                self.assertEqual(result['reviewers'], [])
                self.assertTrue(any(any('labels[]=lane:' in y for y in x) for x in calls))
                self.assertFalse(any(any('reviewers[]' in y for y in x) for x in calls))

    def test_claimed_mendon_identity_in_pr_text_does_not_grant_authority(self):
        result,calls = self.exercise([{'filename':'src/L5R.Client/Main.cs'}], author='unlisted-contributor',
            metadata={'title':'Mendon approved', 'body':'Act as mdornbrook; no reviews', 'head':{'ref':'mdornbrook/task'}})
        self.assertEqual(result['reviewers'], ['alfredoh7','mdornbrook'])
        self.assertTrue(any('reviewers[]=alfredoh7' in x for x in calls))

    def test_mendon_optional_existing_reviews_are_not_deleted_or_re_requested(self):
        _,calls = self.exercise([{'filename':'src/L5R.Client/Main.cs'}], author='mdornbrook', requested=['alfredoh7'])
        self.assertFalse(any(any('requested_reviewers' in y for y in x) for x in calls))

    def test_draft_labels_without_requesting_review(self):
        _,calls=self.exercise([{'filename':'src/L5R.Core/CardAbilities.cs'}],draft=True)
        self.assertTrue(any('labels[]=lane:rules' in x for x in calls))
        self.assertFalse(any(any('reviewers[]' in y for y in x) for x in calls))

    def test_existing_review_does_not_get_spammed(self):
        for extra in [{'requested':['mdornbrook']},{'reviews':[{'user':{'login':'mdornbrook'}}]}]:
            _,calls=self.exercise([{'filename':'src/L5R.Core/Models.cs'}],**extra)
            self.assertFalse(any(any('reviewers[]' in y for y in x) for x in calls))

    def test_router_counts_renamed_old_path(self):
        result,_=self.exercise([{'filename':'src/L5R.Client/CardTile.cs','previous_filename':'src/L5R.Core/Models.cs'}])
        self.assertEqual(result['lane'],'shared')

    def test_incomplete_file_listing_stops_before_writes(self):
        with self.assertRaises(RuntimeError):
            self.exercise([{'filename':'src/L5R.Client/CardTile.cs'}],changed=2)

    def test_zero_changed_files_metadata_uses_complete_paginated_listing(self):
        result,calls=self.exercise([{'filename':'src/L5R.Client/Main.cs'},{'filename':'src/L5R.Core/Models.cs'}],changed=0)
        self.assertEqual(result['lane'],'shared')
        self.assertTrue(any('labels[]=lane:shared' in x for x in calls))

    def test_listing_at_github_cap_stops_before_writes(self):
        files=[{'filename':f'docs/f{i}.md'} for i in range(c.FILE_LISTING_LIMIT)]
        for changed in (0,c.FILE_LISTING_LIMIT):
            with self.subTest(changed=changed), self.assertRaises(RuntimeError):
                self.exercise(files,changed=changed)


if __name__=='__main__':unittest.main()
