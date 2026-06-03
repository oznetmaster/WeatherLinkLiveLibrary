// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

document.addEventListener('DOMContentLoaded', () => {
  const article = document.querySelector('article.content.wrap[data-uid]');
  if (!article) {
	return;
  }

  const currentPath = window.location.pathname || '';
  if (!currentPath.includes('/api/')) {
	return;
  }

  const sections = [];
  let currentGroup = null;

  for (const child of Array.from(article.children)) {
	if (child.tagName === 'H3') {
	  currentGroup = { title: child.textContent?.trim() || 'Section', items: [] };
	  sections.push(currentGroup);
	  continue;
	}

	if (child.tagName === 'H4') {
	  const link = child.querySelector('a[href]');
	  if (!link) {
		continue;
	  }

	  if (!currentGroup) {
		currentGroup = { title: 'Contents', items: [] };
		sections.push(currentGroup);
	  }

	  const href = link.getAttribute('href') || '';
	  const text = link.textContent?.trim() || href;
	  currentGroup.items.push({ href, text });
	}
  }

  if (!sections.some(section => section.items.length > 0)) {
	const localHeadings = Array.from(article.querySelectorAll('h2[id], h3[id], h4[id]'));
	if (localHeadings.length === 0) {
	  return;
	}

	sections.push({
	  title: 'On This Page',
	  items: localHeadings.map(heading => ({ href: `#${heading.id}`, text: heading.textContent?.trim() || heading.id }))
	});
  }

  article.classList.add('api-enhanced-layout');

  const sidebar = document.createElement('nav');
  sidebar.className = 'api-sidebar';
  sidebar.setAttribute('aria-label', 'API page navigation');

  const sidebarTitle = document.createElement('div');
  sidebarTitle.className = 'api-sidebar-title';
  sidebarTitle.textContent = 'Index';
  sidebar.appendChild(sidebarTitle);

  for (const section of sections) {
	if (!section.items.length) {
	  continue;
	}

	const group = document.createElement('div');
	group.className = 'api-sidebar-group';

	const heading = document.createElement('div');
	heading.className = 'api-sidebar-group-title';
	heading.textContent = section.title;
	group.appendChild(heading);

	const list = document.createElement('ul');
	list.className = 'api-sidebar-list';

	for (const item of section.items) {
	  const listItem = document.createElement('li');
	  const anchor = document.createElement('a');
	  anchor.href = item.href;
	  anchor.textContent = item.text;
	  if (item.href && currentPath.endsWith(item.href.replace('./', ''))) {
		anchor.classList.add('active');
	  }
	  listItem.appendChild(anchor);
	  list.appendChild(listItem);
	}

	group.appendChild(list);
	sidebar.appendChild(group);
  }

  const main = document.createElement('div');
  main.className = 'api-main';

  while (article.firstChild) {
	main.appendChild(article.firstChild);
  }

  article.appendChild(sidebar);
  article.appendChild(main);
});
